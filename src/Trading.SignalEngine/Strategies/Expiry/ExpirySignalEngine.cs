using Microsoft.Extensions.Logging;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;

namespace Trading.SignalEngine.Strategies.Expiry;

/// <summary>
/// Expiry-day engine — one dedicated engine, mode-gated by time of day, instead of
/// the Directional-vs-HeroZero confidence race StrategyManager used to run on expiry days.
/// </summary>
public class ExpirySignalEngine
{
    private readonly IInstrumentCache _instruments;
    private readonly ExpiryScoreService _scoreService;
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
        ILogger<ExpirySignalEngine> logger)
    {
        _instruments = instruments;
        _scoreService = scoreService;
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
        List<OptionChainData> chain)
    {
        if (chain == null || chain.Count == 0) return null;

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

        if (universe.Count == 0)
        {
            _logger.LogDebug("ExpiryEngine: no strikes for {Symbol} in mode {Mode}", symbol, mode);
            return null;
        }

        var (ceScores, peScores) = _scoreService.Score(
            symbol, universe, ema20, ema50, ema20_5m, ema50_5m, priceChangePct, candles5m);

        var hero = ceScores.Concat(peScores).OrderByDescending(s => s.TotalScore).FirstOrDefault();
        if (hero == null) return null;

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
            "ExpirySignalEngine: {Mode} {Type} {Symbol} Strike:{Strike} Ltp:{Ltp} Score:{Score:F0} " +
            "(Trend:{Trend:F0} Volume:{Vol:F0} Gamma:{Gamma:F0} OiShift:{Oi:F0} Momentum:{Mom:F0})",
            mode, hero.OptionType, symbol, hero.StrikePrice, hero.Ltp, hero.TotalScore,
            hero.TrendScore, hero.VolumeScore, hero.GammaScore, hero.OiShiftScore, hero.PremiumMomentumScore);

        var builder = new SignalBuilder(symbol);
        if (call) builder.CALL(); else builder.PUT();

        return builder
            .AtIndexPrice(ltp)
            .WithIndexStops(_instruments.GetIndexStopPoints(symbol))
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
