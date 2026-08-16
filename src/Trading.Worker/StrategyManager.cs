using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Scoring.Ranking;
using Trading.SignalEngine.Scoring.Services;
using Trading.SignalEngine.Services;
using Trading.SignalEngine.Strategies;
using Trading.SignalEngine.Strategies.Expiry;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

/// <summary>
/// Single entry point for engine selection.
/// SignalWorker calls SelectSignalAsync() — builds a StrategyContext and delegates the
/// actual decision to StrategySelectorService (the "one brain"), then dispatches to
/// whichever concrete engine it picked: Expiry | HeroZero | Directional | none.
/// </summary>
/// <summary>
/// Additive result wrapper around SelectSignalAsync — carries MarketState/Strategy/Confidence/
/// BlockedReason for the Market Snapshot Audit feature even when Signal is null (suppressed by
/// StrategySelectorService, the confidence gate, or lot sizing). Does not change any decision made
/// inside SelectSignalAsync — only what gets returned to the caller.
/// </summary>
public record SignalSelectionResult(
    Signal? Signal,
    MarketState MarketState,
    SelectedStrategy Strategy,
    decimal Confidence,
    string? BlockedReason,
    string? BlockedBy,
    SignalScore? Score = null);

public class StrategyManager
{
    private readonly HeroZeroScoreService _heroScorer;
    private readonly StrikeRankingService _heroRanking;
    private readonly StrategySelectorService _selector;
    private readonly MarketClassificationService _classifier;
    private readonly ExpirySignalEngine _expiryEngine;
    private readonly AdaptiveLotSizingService _lotSizing;
    private readonly IRepository<StrategyExecutionLog> _executionLogRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<StrategyManager> _logger;

    public StrategyManager(
        HeroZeroScoreService heroScorer,
        StrikeRankingService heroRanking,
        StrategySelectorService selector,
        MarketClassificationService classifier,
        ExpirySignalEngine expiryEngine,
        AdaptiveLotSizingService lotSizing,
        IRepository<StrategyExecutionLog> executionLogRepo,
        IUnitOfWork uow,
        ILogger<StrategyManager> logger)
    {
        _heroScorer = heroScorer;
        _heroRanking = heroRanking;
        _selector = selector;
        _classifier = classifier;
        _expiryEngine = expiryEngine;
        _lotSizing = lotSizing;
        _executionLogRepo = executionLogRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<SignalSelectionResult> SelectSignalAsync(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        var strategyContext = await BuildStrategyContextAsync(ctx, market, symbol);
        var decision = await _selector.Select(strategyContext);

        var engineResult = decision.Strategy switch
        {
            SelectedStrategy.Expiry => await RunExpiry(ctx, market, symbol),
            SelectedStrategy.HeroZero => await RunHeroZero(ctx, market, symbol),
            SelectedStrategy.Directional => RunDirectional(ctx, market, symbol),
            _ => SignalGenerationResult.Failed(decision.Reason)
        };

        // StrategySelector only ever gets blamed for "no strategy chosen" (Strategy == None).
        // Once a concrete engine actually ran, any further suppression — the engine's own
        // conditions, the confidence gate, or lot sizing — is the SignalEngine's call.
        var blockedBy = decision.Strategy == SelectedStrategy.None ? "StrategySelector" : "SignalEngine";

        if (engineResult.Signal == null)
        {
            var reason = engineResult.FailureReasons.Count > 0
                ? string.Join(" | ", engineResult.FailureReasons)
                : decision.Reason;
            return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, 0m, reason, blockedBy,
                engineResult.Score);
        }

        var signal = engineResult.Signal;
        if (signal.Confidence < 70m)
        {
            return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, signal.Confidence,
                $"Confidence {signal.Confidence:F0} below minimum 70", blockedBy, engineResult.Score);
        }

        var lotDecision = await _lotSizing.DetermineAsync(ctx, market, signal.Confidence);
        if (lotDecision.Lots <= 0)
        {
            return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, signal.Confidence,
                lotDecision.Reason, blockedBy, engineResult.Score);
        }
        signal.Quantity = lotDecision.Lots;
        signal.SelectedStrategy = decision.Strategy.ToString();
        signal.MarketState = decision.MarketState.ToString();
        signal.IsExpiryDay = strategyContext.IsExpiryDay;

        await LogExecutionAsync(symbol, strategyContext, decision, signal);

        return new SignalSelectionResult(signal, decision.MarketState, decision.Strategy, signal.Confidence, null, null,
            engineResult.Score);
    }

    // ── Explainable decision summary — MarketState/strategy/indicators/historical win rate/confidence ──
    private static string BuildExplainableReason(StrategyContext ctx, StrategyDecision decision, Signal signal)
    {
        var emaDescriptor = decision.MarketState switch
        {
            MarketState.StrongTrend => "Strong",
            MarketState.Trend => "Trend",
            _ => "Flat"
        };

        var historical = decision.HistoricalWinRate.HasValue
            ? $"{decision.HistoricalWinRate.Value:F0}% (n={decision.HistoricalTrades})"
            : "n/a";

        return $"MarketState={decision.MarketState} Selected={decision.Strategy} EMA={emaDescriptor} " +
               $"ATR={ctx.Atr:F2} RSI={ctx.Rsi:F0} PCR={ctx.Pcr:F2} HistoricalWinRate={historical} " +
               $"Confidence={signal.Confidence:F0}";
    }

    // ── Build the pure decision snapshot ─────────────────────────────────────
    private async Task<StrategyContext> BuildStrategyContextAsync(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        var isHighImpactEvent = market.NewsEvent != null;
        var isExpiryDay = ctx.Instruments.IsExpiryDay(symbol);

        // Computed once here so the selector itself can stay a pure function over primitives —
        // HeroZeroSignalEngine.Generate re-scores when it actually runs; the small duplicate
        // work is the accepted tradeoff for keeping StrategySelectorService dependency-free.
        var settings = await ctx.SystemSettings.GetAsync();
        var (ceScores, peScores) = _heroScorer.Score(
            symbol, market.OptionChain, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m,
            market.PriceChangePct, market.Candles5m, settings);
        var heroResult = _heroRanking.Rank(ceScores, peScores);
        var heroTopScore = Math.Max(heroResult.HeroCe?.TotalScore ?? 0m, heroResult.HeroPe?.TotalScore ?? 0m);

        var volumeRatio = market.AvgVolume20 > 0 ? market.CurrentVolume / market.AvgVolume20 : 0m;
        var classification = _classifier.Classify(
            symbol, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m,
            market.AtrRaw, market.Rsi, market.Vwap, market.Ltp, volumeRatio, market.PriceChangePct,
            market.MarketRegime.IsGapUp, market.MarketRegime.IsGapDown, isHighImpactEvent, settings);

        return new StrategyContext
        {
            Symbol = symbol,
            Ltp = market.Ltp,
            Ema20 = market.Ema20,
            Ema50 = market.Ema50,
            Rsi = market.Rsi,
            Pcr = market.PcrValue,
            Atr = market.AtrRaw,
            IsExpiryDay = isExpiryDay,
            IsHighImpactEvent = isHighImpactEvent,
            Regime = market.MarketRegime,
            OptionChain = market.OptionChain,
            HeroZeroTopScore = heroTopScore,
            MarketState = classification.State,
            MarketStateReason = classification.Reason
        };
    }

    // ── Engine runners ────────────────────────────────────────────────────────
    private SignalGenerationResult RunDirectional(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        // Entry trigger candle: 5m EMA stays the trend filter regardless of this setting —
        // only which candle drives body%/direction confirmation changes. Default follows
        // trading mode (Paper trials the 1m trigger, Live keeps the proven 5m one) until
        // this StrategySetting is explicitly set either way.
        var defaultToOneMin = ctx.Orders.CurrentMode == TradingMode.Live;
        var useOneMinTrigger = ctx.StrategySettings.GetBool("UseOneMinuteEntryCandle", symbol, defaultToOneMin);
        var triggerCandle = useOneMinTrigger ? market.LastClosed1mCandle : market.LastClosed5mCandle;

        // Confidence gate is applied once, centrally, in SelectSignalAsync — not here.
        return ctx.Engine.GenerateSignalDetailed(
            symbol, market.Ltp, market.Ema20, market.Ema50,
            market.PcrValue, market.Rsi, market.CurrentVolume, market.AvgVolume20,
            market.PriceChangePct, market.AtrPct, market.AtrRaw, market.OptionChain,
            market.StrategyHint, market.Ema20_5m, market.Ema50_5m, triggerCandle);
    }

    private async Task<SignalGenerationResult> RunExpiry(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        // Settings fetch mirrors BuildStrategyContextAsync's — cached 60s, not a fresh DB hit in practice.
        var settings = await ctx.SystemSettings.GetAsync();
        var signal = _expiryEngine.Generate(
            symbol, market.Ltp, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m,
            market.PriceChangePct, market.Candles5m, market.OptionChain,
            market.Rsi, market.PcrValue, market.AtrRaw, market.CurrentVolume, market.AvgVolume20, settings);
        return signal != null
            ? SignalGenerationResult.Success(signal)
            : SignalGenerationResult.Failed("Expiry engine did not generate a signal");
    }

    private async Task<SignalGenerationResult> RunHeroZero(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        var signal = await ctx.HeroZeroEngine.Generate(
            symbol, market.Ltp, market.Ema20, market.Ema50,
            market.PcrValue, market.Rsi, market.CurrentVolume, market.AvgVolume20,
            market.PriceChangePct, market.AtrRaw, market.Ema20_5m, market.Ema50_5m, market.Candles5m,
            ctx.Instruments.IsExpiryDay(symbol), market.OptionChain);
        return signal != null
            ? SignalGenerationResult.Success(signal)
            : SignalGenerationResult.Failed("HeroZero engine did not generate a signal");
    }

    // ── Audit log — best-effort, never blocks signal generation ──────────────
    private async Task LogExecutionAsync(string symbol, StrategyContext strategyContext, StrategyDecision decision, Signal signal)
    {
        var explainable = BuildExplainableReason(strategyContext, decision, signal);

        try
        {
            await _executionLogRepo.AddAsync(new StrategyExecutionLog
            {
                CreatedOn = IstClock.Now,
                Symbol = symbol,
                SelectedStrategy = decision.Strategy.ToString(),
                MarketState = decision.MarketState.ToString(),
                Reason = explainable,
                Score = signal.Confidence
            });
            await _uow.SaveChangesAsync();
        }
        catch (Exception)
        {
        }
    }

}
