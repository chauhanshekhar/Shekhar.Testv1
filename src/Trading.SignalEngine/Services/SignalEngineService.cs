using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.SignalEngine.Services;

public class SignalEngineService
{
    private readonly IInstrumentCache _instrumentCache;
    private readonly SystemSettingsService _settingsService;
    private readonly OptionTargetService _optionTargets;
    private readonly IRepository<OptionChainData> _optionChainRepo;
    private readonly ILogger<SignalEngineService> _logger;

    public SignalEngineService(
        IInstrumentCache instrumentCache,
        SystemSettingsService settingsService,
        OptionTargetService optionTargets,
        IRepository<OptionChainData> optionChainRepo,
        ILogger<SignalEngineService> logger)
    {
        _instrumentCache = instrumentCache;
        _settingsService = settingsService;
        _optionTargets = optionTargets;
        _optionChainRepo = optionChainRepo;
        _logger = logger;

    }

    // Resolved once per GenerateSignal call — cached 60s in SystemSettingsService
    private TradingSettings Settings => _settingsService.GetAsync().GetAwaiter().GetResult();


    private static decimal ComputeConfidence(
    bool buy,
    decimal ema20,
    decimal ema50,
    decimal ltp,
    decimal pcr,
    decimal rsi,
    decimal currentVolume,
    decimal avgVolume20,
    decimal bodyPct)
    {
        decimal score = 25m; // base score

        // EMA strength
        var emaSepPct = ema50 > 0
            ? Math.Abs(ema20 - ema50) / ema50 * 100m
            : 0m;

        if (emaSepPct >= 0.20m)
        {
            score += 20m;
        }
        else if (emaSepPct >= 0.10m)
        {
            score += 15m;
        }
        else if (emaSepPct >= 0.05m)
        {
            score += 8m;
        }
        else
        {
            score += 0m;
        }

        // PCR
        if (buy)
            score += pcr > 1.20m ? 15m :
                     pcr > 1.05m ? 8m : 0m;
        else
            score += pcr < 0.80m ? 15m :
                     pcr < 0.95m ? 8m : 0m;

        // RSI
        if (buy)
            score += rsi > 70m ? 15m :
                     rsi > 55m ? 8m : 0m;
        else
            score += rsi < 30m ? 15m :
                     rsi < 45m ? 8m : 0m;

        // Volume (optional)
        if (currentVolume > 0 && avgVolume20 > 0)
        {
            var volRatio = currentVolume / avgVolume20;

            score += volRatio > 2m ? 10m :
                     volRatio > 1.5m ? 5m : 0m;
        }

        // Candle quality
        if (bodyPct >= 70m)
        {
            score += 10m;
        }
        else if (bodyPct >= 55m)
        {
            score += 5m;
        }

        return Math.Min(score, 99m);
    }

    private bool IsLiquid(string symbol, OptionChainData o)
    {
        var minVolume = Settings.LiquidityMinVolume.GetValueOrDefault(symbol, 50_000);
        var minOi = Settings.LiquidityMinOI.GetValueOrDefault(symbol, 10_000);
        var minLtp = Settings.LiquidityMinLtp.GetValueOrDefault(symbol, 15m);
        var maxLtp = Settings.LiquidityMaxLtp.GetValueOrDefault(symbol, 300m);

        return o.Volume >= minVolume &&
               o.OpenInterest >= minOi &&
               o.Ltp >= minLtp &&
               o.Ltp <= maxLtp;
    }
    // Lot scaling: only increase size when confidence is very high.
    private static int LotsForConfidence(decimal confidence) => confidence switch
    {
        > 90m => 3,
        > 80m => 2,
        _ => 1,
    };

    public Signal? GenerateSignal(
        string symbol,
        decimal ltp,
        decimal ema20,
        decimal ema50,
        decimal pcr,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume20,
        decimal priceChangePct,
        decimal atrPct,
        decimal atrRaw = 0,
        List<OptionChainData>? optionChain = null,
        string strategyHint = TradingConst.StrategyType.Auto,
        decimal ema20_5m = 0,
        decimal ema50_5m = 0,
        Candle? lastClosedCandle = null)
    {
        // No chain data = no delta confirmation = no signal.
        if (optionChain == null || optionChain.Count == 0)
            return null;

        // ATR% filter — suppress signals in low-volatility/sideways markets.
        // Threshold is instrument-specific since indices differ in volatility.
        atrPct = ltp > 0
    ? atrRaw / ltp * 100m
    : 0m;

        var minAtr = Settings.MinAtrPercent.GetValueOrDefault(symbol, 0.10m);

        if (atrPct < minAtr)
            return null;
        if (lastClosedCandle == null)
            return null;

        var range =
            lastClosedCandle.High -
            lastClosedCandle.Low;

        if (range <= 0)
            return null;

        var bodyPct =
            Math.Abs(lastClosedCandle.Close - lastClosedCandle.Open)
            / range * 100m;

        if (bodyPct < Settings.MinBodyPercent)
            return null;

        var interval = (decimal)_instrumentCache.GetStrikeInterval(symbol);
        var spot = optionChain
      .Where(x => x.UnderlyingLtp > 0)
      .Select(x => x.UnderlyingLtp)
      .FirstOrDefault();

        if (spot <= 0)
            spot = ltp;

        var atmStrike =
      Math.Round(
          spot / interval,
          MidpointRounding.AwayFromZero)
      * interval;

        var chain = optionChain!;
        var atmCe = chain
       .Where(x =>
           x.OptionType == TradingConst.OptionType.CE &&
           IsLiquid(symbol, x))
       .OrderBy(x => Math.Abs(x.StrikePrice - atmStrike))
       .ThenByDescending(x => x.Volume)
       .ThenByDescending(x => x.OpenInterest)
       .FirstOrDefault();

        var atmPe = chain
            .Where(x =>
                x.OptionType == TradingConst.OptionType.PE &&
                IsLiquid(symbol, x))
            .OrderBy(x => Math.Abs(x.StrikePrice - atmStrike))
            .ThenByDescending(x => x.Volume)
            .ThenByDescending(x => x.OpenInterest)
            .FirstOrDefault();

        if (atmCe == null || atmPe == null)
            return null;

        var ceAtmDelta = atmCe.Delta;
        var peAtmDelta = atmPe.Delta;
        bool trendBullish =
        ema20_5m > 0 &&
        ema50_5m > 0 &&
        ema20_5m > ema50_5m;

        bool trendBearish =
            ema20_5m > 0 &&
            ema50_5m > 0 &&
            ema20_5m < ema50_5m;
        //    var trendBullish =
        //ema20_5m > ema50_5m;

        //    var trendBearish =
        //        ema20_5m < ema50_5m;
        _logger.LogInformation(
    """
    CALL:
    trendBullish={trendBullish}
    ltp>ema20={ltpEma}
    ema20>ema50={emaCross}
    pcr={pcr}
    rsi={rsi}
    ceDelta={ceDelta}
    priceChangePct={priceChangePct}
    ceOi={ceOi}

    PUT:
    trendBearish={trendBearish}
    ltp<ema20={ltpBelow}
    ema20<ema50={emaBelow}
    peDelta={peDelta}
    peOi={peOi}
    """,
    trendBullish,
    ltp > ema20,
    ema20 > ema50,
    pcr,
    rsi,
    ceAtmDelta,
    priceChangePct,
    atmCe.ChangeInOi,

    trendBearish,
    ltp < ema20,
    ema20 < ema50,
    peAtmDelta,
    atmPe.ChangeInOi
);

        // Candle body confirmation — filters weak/indecisive closed 5m candles.
        // Body must be at least half the total range in the direction of the trade.
        bool candleBullish =
     lastClosedCandle != null &&
     lastClosedCandle.Close > lastClosedCandle.Open;


        bool candleBearish =
            lastClosedCandle != null &&
            lastClosedCandle.Close < lastClosedCandle.Open;
        var volConfirmed = currentVolume > avgVolume20 * 1.5m;
        // ── Entry: 1m timing + 5m trend alignment + candle strength ──────────
        bool call =
     trendBullish &&
     ltp > ema20 &&
     ema20 > ema50 &&
     pcr >= 1.05m &&
     rsi > 55m &&
     rsi < 75m &&
     ceAtmDelta >= 0.40m &&
     volConfirmed &&
     priceChangePct > 0.15m &&
     candleBullish;

        bool put =
            trendBearish &&
            ltp < ema20 &&
            ema20 < ema50 &&
            pcr <= 0.95m &&
            rsi < 45m &&
            rsi > 30m &&
            peAtmDelta <= -0.40m &&
            volConfirmed &&
            priceChangePct < -0.15m &&
            candleBearish;
        if (!call && !put)
            return null;

        var optionType = call ? TradingConst.OptionType.CE : TradingConst.OptionType.PE;

        // Single lookup — LTP + TradingSymbol from chain
        var chainLeg = chain
    .Where(o =>
        o.OptionType == optionType &&
        IsLiquid(symbol, o))
    .OrderBy(o => Math.Abs(o.StrikePrice - atmStrike))
    .ThenByDescending(o => o.Volume)
    .ThenByDescending(o => o.OpenInterest)
    .FirstOrDefault();
        var optionLtp = chainLeg?.Ltp ?? 0m;

        // No valid premium — chain data stale or strike not found. Block signal.
        if (optionLtp <= 0)
            return null;

        // Prefer Groww's TradingSymbol; fall back to constructed name
        var optionSymbol = (chainLeg != null && !string.IsNullOrWhiteSpace(chainLeg.TradingSymbol))
            ? chainLeg.TradingSymbol
            : $"{symbol}{IstClock.Now:yyMMM}{(int)atmStrike}{optionType}".ToUpper();

        // ATR-based SL: at least 1.5× ATR to avoid being stopped by normal candle noise.
        // Falls back to fixed minimum when ATR is not available.
        decimal fixedMin = _instrumentCache.GetIndexStopPoints(symbol);
        decimal slPoints = atrRaw > 0
            ? Math.Max(fixedMin, Math.Round(atrRaw * Settings.AtrSlMultiplier, 0))
            : fixedMin;
        var confidence = ComputeConfidence(call, ema20, ema50, ltp, pcr, rsi, currentVolume, avgVolume20, bodyPct);

        var vega = chainLeg?.Vega ?? 0m;
        var highVegaThreshold = Settings.HighVegaThreshold;

        var lots = LotsForConfidence(confidence);
        if (Math.Abs(vega) > highVegaThreshold && lots > 1) lots--;


        // ── Build Signal using SignalBuilder ──────────────────────────────────
        // Buy()/Sell() MUST be called before WithIndexStops() — stops depend on direction.
        decimal sl;
        decimal t1;
        decimal t2;

        if (call)
        {
            sl = ltp - slPoints;

            // realistic intraday move
            t1 = ltp + atrRaw;
            t2 = ltp + (atrRaw * 2);
        }
        else
        {
            sl = ltp + slPoints;

            t1 = ltp - atrRaw;
            t2 = ltp - (atrRaw * 2);
        }


        // ===============================
        // OPTION Targets — option's OWN candles (ATR + support/resistance) first;
        // falls back to a delta-adjusted index-based estimate only when there isn't
        // enough option-candle history yet (e.g. just after open, or a strike picked
        // for the first time today).
        // ===============================

        decimal optionSL;
        decimal optionT1;
        decimal optionT2;

        var optionSnapshots = _optionChainRepo
            .FindAsync(o => o.TradingSymbol == optionSymbol && o.CreatedOn >= IstClock.Now.AddHours(-2))
            .GetAwaiter().GetResult();
        var optionCandles = OptionTargetService.BuildCandlesFromSnapshots(optionSnapshots, optionSymbol);
        var candleLevels = _optionTargets.Calculate(optionCandles, optionLtp);

        if (candleLevels != null)
        {
            optionSL = candleLevels.StopLoss;
            optionT1 = candleLevels.Target1;
            optionT2 = candleLevels.Target2;
        }
        else
        {
            // Fallback: delta-adjusted, same distance basis as index SL (slPoints).
            var delta = Math.Abs(chainLeg.Delta);
            if (delta <= 0) delta = 0.45m;

            decimal optionMove1 = Math.Abs(t1 - ltp) * delta;
            decimal optionMove2 = Math.Abs(t2 - ltp) * delta;
            var optionRisk = slPoints * delta;

            optionSL = Math.Round(optionLtp - optionRisk, 2);

            var maxLoss = optionLtp * 0.80m;
            if (optionSL < maxLoss) optionSL = maxLoss;

            optionT1 = Math.Round(optionLtp + optionMove1, 2);
            optionT2 = Math.Round(optionLtp + optionMove2, 2);
        }

        // ===============================
        // Build Signal
        // ===============================

        var builder = new SignalBuilder(symbol);

        if (call)
            builder.CALL();
        else
            builder.PUT();


        builder
            .AtIndexPrice(ltp)

            .WithIndexTargets(
                sl,
                t1,
                t2)

            .ForOption(
                atmStrike,
                optionType,
                optionSymbol,
                optionLtp,
                chainLeg.Delta)

            .WithOptionTargets(
                optionSL,
                optionT1,
                optionT2)

            .OnExchange(
                _instrumentCache.GetExchange(symbol))

            .WithLots(lots)

            .WithConfidence(confidence);


        return builder.Build();
    }
}
