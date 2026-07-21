using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
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
    private readonly IStrategySettingsService _strategySettings;
    private readonly OptionTargetService _optionTargets;
    private readonly AtrRiskService _atrRisk;
    private readonly IRepository<OptionChainData> _optionChainRepo;
    private readonly ILogger<SignalEngineService> _logger;

    public SignalEngineService(
        IInstrumentCache instrumentCache,
        SystemSettingsService settingsService,
        IStrategySettingsService strategySettings,
        OptionTargetService optionTargets,
        AtrRiskService atrRisk,
        IRepository<OptionChainData> optionChainRepo,
        ILogger<SignalEngineService> logger)
    {
        _instrumentCache = instrumentCache;
        _settingsService = settingsService;
        _strategySettings = strategySettings;
        _optionTargets = optionTargets;
        _atrRisk = atrRisk;
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
        => GenerateSignalDetailed(
            symbol, ltp, ema20, ema50, pcr, rsi, currentVolume, avgVolume20,
            priceChangePct, atrPct, atrRaw, optionChain, strategyHint,
            ema20_5m, ema50_5m, lastClosedCandle).Signal;

    // Same decision logic as GenerateSignal — additionally reports every failed condition
    // (instead of a bare null) so callers can audit/log exactly why a signal did not fire.
    public SignalGenerationResult GenerateSignalDetailed(
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
            return SignalGenerationResult.Failed("No option chain data available");

        // ATR% filter — suppress signals in low-volatility/sideways markets.
        // Threshold is instrument-specific since indices differ in volatility.
        atrPct = ltp > 0
    ? atrRaw / ltp * 100m
    : 0m;

        var minAtr = _strategySettings.GetDecimal("MinAtrPercent", symbol, 0.10m);

        if (atrPct < minAtr)
            return SignalGenerationResult.Failed($"ATR% {atrPct:F2} below minimum {minAtr:F2}");
        if (lastClosedCandle == null)
            return SignalGenerationResult.Failed("No closed 5m candle available");

        var range =
            lastClosedCandle.High -
            lastClosedCandle.Low;

        if (range <= 0)
            return SignalGenerationResult.Failed("Candle range invalid (High <= Low)");

        var bodyPct =
            Math.Abs(lastClosedCandle.Close - lastClosedCandle.Open)
            / range * 100m;

        if (bodyPct < Settings.MinBodyPercent)
            return SignalGenerationResult.Failed($"Candle body {bodyPct:F1}% below minimum {Settings.MinBodyPercent}%");

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
            return SignalGenerationResult.Failed("No liquid ATM CE/PE strikes found");

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
        // Each sub-condition is named below (same operands/thresholds, just factored out of the
        // single boolean expression) purely so a rejection can report exactly which ones failed —
        // the values of `call`/`put` themselves are unchanged.
        bool condTrendBullish = trendBullish;
        bool condLtpAboveEma20 = ltp > ema20;
        bool condEma20AboveEma50 = ema20 > ema50;
        bool condPcrCall = pcr >= 1.05m;
        bool condRsiCall = rsi > 55m && rsi < 75m;
        bool condDeltaCall = ceAtmDelta >= 0.40m;
        bool condVolumeCall = volConfirmed;
        bool condPriceChangeCall = priceChangePct > 0.15m;
        bool condCandleBullish = candleBullish;

        bool call =
     condTrendBullish &&
     condLtpAboveEma20 &&
     condEma20AboveEma50 &&
     condPcrCall &&
     condRsiCall &&
     condDeltaCall &&
     condVolumeCall &&
     condPriceChangeCall &&
     condCandleBullish;

        bool condTrendBearish = trendBearish;
        bool condLtpBelowEma20 = ltp < ema20;
        bool condEma20BelowEma50 = ema20 < ema50;
        bool condPcrPut = pcr <= 0.95m;
        bool condRsiPut = rsi < 45m && rsi > 30m;
        bool condDeltaPut = peAtmDelta <= -0.40m;
        bool condVolumePut = volConfirmed;
        bool condPriceChangePut = priceChangePct < -0.15m;
        bool condCandleBearish = candleBearish;

        bool put =
            condTrendBearish &&
            condLtpBelowEma20 &&
            condEma20BelowEma50 &&
            condPcrPut &&
            condRsiPut &&
            condDeltaPut &&
            condVolumePut &&
            condPriceChangePut &&
            condCandleBearish;

        if (!call && !put)
        {
            var callChecks = new (string Label, bool Passed)[]
            {
                ("Trend Bullish", condTrendBullish),
                ("LTP > EMA20", condLtpAboveEma20),
                ("EMA20 > EMA50", condEma20AboveEma50),
                ("PCR >= 1.05", condPcrCall),
                ("RSI in (55,75)", condRsiCall),
                ("CE Delta >= 0.40", condDeltaCall),
                ("Volume confirmed", condVolumeCall),
                ("PriceChangePct > 0.15", condPriceChangeCall),
                ("Candle bullish", condCandleBullish),
            };
            var putChecks = new (string Label, bool Passed)[]
            {
                ("Trend Bearish", condTrendBearish),
                ("LTP < EMA20", condLtpBelowEma20),
                ("EMA20 < EMA50", condEma20BelowEma50),
                ("PCR <= 0.95", condPcrPut),
                ("RSI in (30,45)", condRsiPut),
                ("PE Delta <= -0.40", condDeltaPut),
                ("Volume confirmed", condVolumePut),
                ("PriceChangePct < -0.15", condPriceChangePut),
                ("Candle bearish", condCandleBearish),
            };

            var failureReasons = callChecks.Where(c => !c.Passed).Select(c => $"CALL: {c.Label}")
                .Concat(putChecks.Where(c => !c.Passed).Select(c => $"PUT: {c.Label}"))
                .ToList();

            _logger.LogInformation(
                "Signal rejected for {Symbol}\nFailed conditions:\n{CallChecklist}\n{PutChecklist}\nRejected because:\n{Reasons}",
                symbol,
                FormatChecklist(callChecks),
                FormatChecklist(putChecks),
                string.Join(Environment.NewLine, failureReasons));

            return SignalGenerationResult.Failed(failureReasons);
        }
        _logger.LogInformation(
    "Signal Check: Symbol={Symbol}, RSI={Rsi}, PCR={Pcr}, Delta={Delta}, TrendBullish={TrendBullish}, TrendBearish={TrendBearish}, Vol={VolConfirmed}, CandleBullish={CandleBullish}, CandleBearish={CandleBearish}",
    symbol,
    rsi,
    pcr,
    ceAtmDelta,
    trendBullish,
    trendBearish,
    volConfirmed,
    candleBullish,
    candleBearish);
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
            return SignalGenerationResult.Failed("Stale/zero option premium for selected leg");

        // Prefer Groww's TradingSymbol; fall back to constructed name
        var optionSymbol = (chainLeg != null && !string.IsNullOrWhiteSpace(chainLeg.TradingSymbol))
            ? chainLeg.TradingSymbol
            : $"{symbol}{IstClock.Now:yyMMM}{(int)atmStrike}{optionType}".ToUpper();

        var confidence = ComputeConfidence(call, ema20, ema50, ltp, pcr, rsi, currentVolume, avgVolume20, bodyPct);

        var vega = chainLeg?.Vega ?? 0m;
        var highVegaThreshold = Settings.HighVegaThreshold;

        var lots = LotsForConfidence(confidence);
        if (Math.Abs(vega) > highVegaThreshold && lots > 1) lots--;


        // ── Build Signal using SignalBuilder ──────────────────────────────────
        // Buy()/Sell() MUST be called before WithIndexStops() — stops depend on direction.
        var risk = _atrRisk.CalculateIndexRisk(symbol, ltp, atrRaw, call, Settings);
        decimal sl = risk.StopLoss;
        decimal t1 = risk.Target1;
        decimal t2 = risk.Target2;

        // Distance-only reuse below (option-target fallback) — unchanged from before.
        decimal slPoints = Math.Abs(ltp - sl);


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


        return SignalGenerationResult.Success(builder.Build());
    }

    private static string FormatChecklist(IEnumerable<(string Label, bool Passed)> items) =>
        string.Join(Environment.NewLine, items.Select(i => $"{(i.Passed ? "✓" : "✗")} {i.Label}"));
}
