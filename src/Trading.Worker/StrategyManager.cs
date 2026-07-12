using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Scoring.Ranking;
using Trading.SignalEngine.Scoring.Services;
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
public class StrategyManager
{
    private readonly HeroZeroScoreService _heroScorer;
    private readonly StrikeRankingService _heroRanking;
    private readonly StrategySelectorService _selector;
    private readonly ExpirySignalEngine _expiryEngine;
    private readonly IRepository<StrategyExecutionLog> _executionLogRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<StrategyManager> _logger;

    public StrategyManager(
        HeroZeroScoreService heroScorer,
        StrikeRankingService heroRanking,
        StrategySelectorService selector,
        ExpirySignalEngine expiryEngine,
        IRepository<StrategyExecutionLog> executionLogRepo,
        IUnitOfWork uow,
        ILogger<StrategyManager> logger)
    {
        _heroScorer = heroScorer;
        _heroRanking = heroRanking;
        _selector = selector;
        _expiryEngine = expiryEngine;
        _executionLogRepo = executionLogRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Signal?> SelectSignalAsync(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        var strategyContext = await BuildStrategyContextAsync(ctx, market, symbol);
        var decision = _selector.Select(strategyContext);

        _logger.LogInformation("StrategyManager → {Strategy} [{Symbol}] — {Reason}",
            decision.Strategy, symbol, decision.Reason);

        Signal? signal = decision.Strategy switch
        {
            SelectedStrategy.Expiry => _expiryEngine.Generate(
                symbol, market.Ltp, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m,
                market.PriceChangePct, market.Candles5m, market.OptionChain),
            SelectedStrategy.HeroZero => await RunHeroZero(ctx, market, symbol),
            SelectedStrategy.Directional => RunDirectional(ctx, market, symbol),
            _ => null
        };

        await LogExecutionAsync(symbol, decision, signal);

        return signal;
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
            HeroZeroTopScore = heroTopScore
        };
    }

    // ── Engine runners ────────────────────────────────────────────────────────
    private Signal? RunDirectional(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        var signal = ctx.Engine.GenerateSignal(
            symbol, market.Ltp, market.Ema20, market.Ema50,
            market.PcrValue, market.Rsi, market.CurrentVolume, market.AvgVolume20,
            market.PriceChangePct, market.AtrPct, market.AtrRaw, market.OptionChain,
            market.StrategyHint, market.Ema20_5m, market.Ema50_5m, market.LastClosed5mCandle);

        if (signal == null) return null;

        if (signal.Confidence < 70m)
        {
            _logger.LogInformation("Directional: low confidence {C:F0} — suppressed [{Symbol}]",
                signal.Confidence, symbol);
            return null;
        }

        return signal;
    }

    private Task<Signal?> RunHeroZero(SignalContext ctx, MarketSnapshot market, string symbol)
        => ctx.HeroZeroEngine.Generate(
            symbol, market.Ltp, market.Ema20, market.Ema50,
            market.PcrValue, market.Rsi, market.CurrentVolume, market.AvgVolume20,
            market.PriceChangePct, market.Ema20_5m, market.Ema50_5m, market.Candles5m,
            ctx.Instruments.IsExpiryDay(symbol), market.OptionChain);

    // ── Audit log — best-effort, never blocks signal generation ──────────────
    private async Task LogExecutionAsync(string symbol, StrategyDecision decision, Signal? signal)
    {
        try
        {
            await _executionLogRepo.AddAsync(new StrategyExecutionLog
            {
                CreatedOn = IstClock.Now,
                Symbol = symbol,
                SelectedStrategy = decision.Strategy.ToString(),
                Reason = decision.Reason,
                Score = signal?.Confidence ?? 0m
            });
            await _uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StrategyManager: failed to persist execution log — continuing");
        }
    }

}
