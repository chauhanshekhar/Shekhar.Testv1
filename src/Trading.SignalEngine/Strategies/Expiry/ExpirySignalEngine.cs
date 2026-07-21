using Microsoft.Extensions.Logging;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;

namespace Trading.SignalEngine.Strategies.Expiry;

/// <summary>
/// Expiry-day engine — one dedicated engine, mode-gated by time of day, instead of
/// the Directional-vs-HeroZero confidence race StrategyManager used to run on expiry days.
/// </summary>
public class ExpirySignalEngine
{
    private readonly IInstrumentCache _instruments;
    private readonly ExpiryScoreService _scoreService;
    private readonly ExpiryMarketScoreService _marketScore;
    private readonly AtrRiskService _atrRisk;
    private readonly ILogger<ExpirySignalEngine> _logger;

    private static readonly TimeSpan GammaBlastStart = new(14, 0, 0);
    private static readonly TimeSpan NormalExpiryStart = new(11, 0, 0);

    private const decimal MorningDeltaMin = 0.45m;
    private const decimal MorningDeltaMax = 0.60m;
    private const decimal GammaBlastMinPremium = 5m;
    private const decimal GammaBlastMaxPremium = 30m;
    private const decimal GammaBlastMinScore = 90m;

    public ExpirySignalEngine(
        IInstrumentCache instruments,
        ExpiryScoreService scoreService,
        ExpiryMarketScoreService marketScore,
        AtrRiskService atrRisk,
        ILogger<ExpirySignalEngine> logger)
    {
        _instruments = instruments;
        _scoreService = scoreService;
        _marketScore = marketScore;
        _atrRisk = atrRisk;
        _logger = logger;
    }

    public Signal? Generate(
        string symbol,
        decimal ltp,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal priceChangePct,
        List<Candle> candles5m,
        List<OptionChainData> chain,
        decimal rsi,
        decimal pcrValue,
        decimal atrRaw,
        decimal currentVolume,
        decimal avgVolume20,
        TradingSettings settings)
    {
        if (chain == null || chain.Count == 0) return null;

        // MarketScore gate — computed BEFORE any option scoring, per side. A side only ever gets
        // scored (universe filtering, Greeks/Liquidity/Trend/Volume per-strike work) if its OWN
        // market score clears the threshold — never score a direction that already failed.
        var ceMarket = _marketScore.Score(symbol, true, ema20, ema50, ema20_5m, ema50_5m, rsi, pcrValue, atrRaw, currentVolume, avgVolume20, priceChangePct);
        var peMarket = _marketScore.Score(symbol, false, ema20, ema50, ema20_5m, ema50_5m, rsi, pcrValue, atrRaw, currentVolume, avgVolume20, priceChangePct);
        var ceQualifies = ceMarket.Normalized >= settings.ExpiryMinMarketScore;
        var peQualifies = peMarket.Normalized >= settings.ExpiryMinMarketScore;

        if (!ceQualifies && !peQualifies)
        {
            _logger.LogDebug(
                "ExpiryEngine: MarketScore CE:{Ce:F0} PE:{Pe:F0} < {Min:F0} — rejected before option scoring ({Symbol})",
                ceMarket.Normalized, peMarket.Normalized, settings.ExpiryMinMarketScore, symbol);
            return null;
        }

        var mode = ResolveMode(IstClock.Now.TimeOfDay);

        var interval = (decimal)_instruments.GetStrikeInterval(symbol);
        var spot = chain.Where(x => x.UnderlyingLtp > 0).Select(x => x.UnderlyingLtp).FirstOrDefault();
        if (spot <= 0) spot = ltp;
        var atmStrike = Math.Round(spot / interval, MidpointRounding.AwayFromZero) * interval;

        var universe = mode switch
        {
            ExpiryMode.MorningProtection => chain
                .Where(o => o.StrikePrice == atmStrike &&
                            Math.Abs(o.Delta) >= MorningDeltaMin &&
                            Math.Abs(o.Delta) <= MorningDeltaMax)
                .ToList(),

            ExpiryMode.NormalExpiry => chain
                .Where(o => Math.Abs(o.StrikePrice - atmStrike) <= interval)
                .ToList(),

            _ => chain // GammaBlast
                .Where(o => o.Ltp >= GammaBlastMinPremium && o.Ltp <= GammaBlastMaxPremium)
                .ToList()
        };

        // Market gate exclusion — a side that didn't qualify above never reaches option scoring at all.
        if (!ceQualifies)
            universe = universe.Where(o => o.OptionType != TradingConst.OptionType.CE).ToList();
        if (!peQualifies)
            universe = universe.Where(o => o.OptionType != TradingConst.OptionType.PE).ToList();

        // Hard disqualifying filters — market-level EMA/RSI, not per-strike. A filter
        // (exclusion from the candidate universe), not a score penalty.
        if (ema20 < ema50 && rsi < 45m)
            universe = universe.Where(o => o.OptionType != TradingConst.OptionType.CE).ToList();
        if (ema20 > ema50 && rsi > 55m)
            universe = universe.Where(o => o.OptionType != TradingConst.OptionType.PE).ToList();

        if (universe.Count == 0)
        {
            _logger.LogDebug("ExpiryEngine: no strikes for {Symbol} in mode {Mode}", symbol, mode);
            return null;
        }

        var (ceScores, peScores) = _scoreService.Score(
            symbol, universe, ema20, ema50, ema20_5m, ema50_5m, priceChangePct, candles5m,
            rsi, pcrValue, atrRaw, currentVolume, avgVolume20, settings);

        var hero = ceScores.Concat(peScores).OrderByDescending(s => s.TotalScore).FirstOrDefault();
        if (hero == null) return null;

        // Defensive backstop, not the primary mechanism — the ceQualifies/peQualifies universe
        // exclusion above already guarantees hero can only come from a qualifying side. Kept anyway
        // as cheap insurance against this invariant ever being violated by a future edit.
        var heroMarket = hero.OptionType == TradingConst.OptionType.CE ? ceMarket : peMarket;
        if (heroMarket.Normalized < settings.ExpiryMinMarketScore)
        {
            _logger.LogDebug(
                "ExpiryEngine: chosen {Type} market score {Score:F0} < {Min:F0} — direction misaligned with gate, rejected ({Symbol})",
                hero.OptionType, heroMarket.Normalized, settings.ExpiryMinMarketScore, symbol);
            return null;
        }

        if (mode == ExpiryMode.GammaBlast && hero.TotalScore <= GammaBlastMinScore)
        {
            _logger.LogDebug("ExpiryEngine: GammaBlast score {Score:F0} <= {Min:F0} — suppressed ({Symbol})",
                hero.TotalScore, GammaBlastMinScore, symbol);
            return null;
        }

        var (slPct, t1Mult, t2Mult) = mode switch
        {
            ExpiryMode.MorningProtection => (0.25m, 1.30m, 1.50m), // SL 25%, Target 50%
            ExpiryMode.NormalExpiry => (0.35m, 1.50m, 2.00m),      // SL 35%, Target 100%
            _ => (0.50m, 2.00m, 5.00m)                             // GammaBlast: SL 50%, T1 2x, T2 5x
        };

        var call = hero.OptionType == TradingConst.OptionType.CE;

        var optionSymbol = !string.IsNullOrWhiteSpace(hero.TradingSymbol)
            ? hero.TradingSymbol
            : $"{symbol}{IstClock.Now:yyMMM}{(int)hero.StrikePrice}{hero.OptionType}".ToUpper();

        _logger.LogInformation(
            "ExpirySignalEngine: {Mode} {Type} {Symbol} Strike:{Strike} Ltp:{Ltp} Confidence:{Score:F0} " +
            "(MarketScore:{Market:F0} [EmaTrend:{EmaTrend:F0} Ema5Trend:{Ema5Trend:F0} RSI:{Rsi:F0} PCR:{Pcr:F0} ATR:{Atr:F0} Volume:{MktVol:F0} Price:{Price:F0}] " +
            "OptionScore:{Option:F0} [Trend:{Trend:F0} Volume:{Vol:F0} Gamma:{Gamma:F0} OiShift:{Oi:F0} Momentum:{Mom:F0} Greeks:{Greeks:F0} Liquidity:{Liq:F0}])",
            mode, hero.OptionType, symbol, hero.StrikePrice, hero.Ltp, hero.TotalScore,
            hero.MarketScore, hero.EmaTrendScore, hero.Ema5TrendScore, hero.RsiScore, hero.PcrScore, hero.AtrScore, hero.MarketVolumeScore, hero.PriceChangeScore,
            hero.OptionScore, hero.TrendScore, hero.VolumeScore, hero.GammaScore, hero.OiShiftScore, hero.PremiumMomentumScore, hero.GreeksScore, hero.LiquidityScore);

        // Index SL/T1/T2 — ATR-based via AtrRiskService (replaces the old fixed
        // GetIndexStopPoints() + WithIndexStops() fallback calc). Option-level
        // slPct/t1Mult/t2Mult below is untouched — that's option risk, not index risk.
        var risk = _atrRisk.CalculateIndexRisk(symbol, ltp, atrRaw, call, settings);

        var builder = new SignalBuilder(symbol);
        if (call) builder.CALL(); else builder.PUT();

        return builder
            .AtIndexPrice(ltp)
            .WithIndexTargets(risk.StopLoss, risk.Target1, risk.Target2)
            .ForOption(hero.StrikePrice, hero.OptionType, optionSymbol, hero.Ltp)
            .WithOptionTargets(
                hero.Ltp * (1 - slPct),
                hero.Ltp * t1Mult,
                hero.Ltp * t2Mult)
            .OnExchange(_instruments.GetExchange(symbol))
            .WithLots(1)
            .WithConfidence(hero.TotalScore)
            .Build();
    }

    private static ExpiryMode ResolveMode(TimeSpan now)
    {
        if (now >= GammaBlastStart) return ExpiryMode.GammaBlast;
        if (now >= NormalExpiryStart) return ExpiryMode.NormalExpiry;
        return ExpiryMode.MorningProtection;
    }
}
