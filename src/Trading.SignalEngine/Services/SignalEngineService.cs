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
    private readonly AtrRiskService _atrRisk;
    private readonly OiAnalysisService _oi;
    private readonly IRepository<OptionChainData> _optionChainRepo;
    private readonly ILogger<SignalEngineService> _logger;

    public SignalEngineService(
        IInstrumentCache instrumentCache,
        SystemSettingsService settingsService,
        IStrategySettingsService strategySettings,
        AtrRiskService atrRisk,
        OiAnalysisService oi,
        IRepository<OptionChainData> optionChainRepo,
        ILogger<SignalEngineService> logger)
    {
        _instrumentCache = instrumentCache;
        _settingsService = settingsService;
        _strategySettings = strategySettings;
        _atrRisk = atrRisk;
        _oi = oi;
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
    decimal bodyPct,
    decimal rsiCallFloor,
    decimal rsiCallCeiling,
    decimal rsiPutFloor,
    decimal rsiPutCeiling,
    decimal bodyPctMinimum)
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
        {
            if (rsi >= rsiCallCeiling)
                score += 15m;
            else if (rsi >= rsiCallFloor)
                score += 8m;
        }
        else
        {
            if (rsi <= rsiPutFloor)
                score += 15m;
            else if (rsi <= rsiPutCeiling)
                score += 8m;
        }

        // Volume (optional)
        if (currentVolume > 0 && avgVolume20 > 0)
        {
            var volRatio = currentVolume / avgVolume20;

            score += volRatio > 2m ? 10m :
                     volRatio > 1.5m ? 5m : 0m;
        }

        // Candle quality
        if (bodyPct >= bodyPctMinimum + 30m)
        {
            score += 10m;
        }
        else if (bodyPct >= bodyPctMinimum)
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
    //
    // Branches on EnableSignalScoring (StrategySettings, per-symbol, default false):
    //   false (default) -> legacy path below, byte-identical to the pre-scoring behavior.
    //   true             -> GenerateScored: CALL and PUT are evaluated independently as scores
    //                       (see SignalScore) instead of one combined AND-chain, so a weak
    //                       secondary factor (body%, ATR, PCR, OI) no longer kills an otherwise
    //                       strong directional setup. Trend/EMA20/EMA50/RSI stay the dominant
    //                       weight in the score, same as they were the hard-required conditions
    //                       before.
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

        var enableSignalScoring = _strategySettings.GetBool("EnableSignalScoring", symbol, false);

        var minAtr = _strategySettings.GetDecimal("MinAtrPercent", symbol, 0.10m);
        var enableAtrFilter = _strategySettings.GetBool("EnableAtrFilter", symbol, true);

        // Legacy-mode-only hard block. In scored mode ATR becomes AtrScore instead — the real
        // safety floor there is AtrGate's own EnableSignalScoring-aware MinAtrPercentAbsolute,
        // which already ran (as a pre-gate) before StrategyManager ever called into this method.
        if (!enableSignalScoring && enableAtrFilter && atrPct < minAtr)
            return SignalGenerationResult.Failed($"ATR% {atrPct:F2} below minimum {minAtr:F2}");

        if (lastClosedCandle == null)
            return SignalGenerationResult.Failed("No closed candle");
        var range = lastClosedCandle.High - lastClosedCandle.Low;
        if (range <= 0)
            return SignalGenerationResult.Failed("Invalid candle range");
        var body = Math.Abs(lastClosedCandle.Close - lastClosedCandle.Open);
        var bodyPct = body / range * 100m;

        var bodyPctMinimum       = _strategySettings.GetDecimal("BodyPctMinimum", symbol, 35m);
        var rsiCallFloor         = _strategySettings.GetDecimal("RsiCallFloor", symbol, 55m);
        var rsiCallCeiling       = _strategySettings.GetDecimal("RsiCallCeiling", symbol, 70m);
        var rsiPutFloor          = _strategySettings.GetDecimal("RsiPutFloor", symbol, 30m);
        var rsiPutCeiling        = _strategySettings.GetDecimal("RsiPutCeiling", symbol, 45m);
        var priceChangeThreshold = _strategySettings.GetDecimal("PriceChangeThreshold", symbol, 0.10m);
        var deltaMinimum         = _strategySettings.GetDecimal("DeltaMinimum", symbol, 0.35m);

        // Contrarian pullback/bounceback RSI band — additive OR alongside the momentum band
        // above. Legacy-mode only (in scored mode RSI is a score, not a gate, so this band is
        // superseded there — a weak-RSI setup can still qualify via other components). Off by
        // default until a StrategySettings row explicitly sets EnableRsiPullbackFilter=1.
        // Backed by missed-opportunity analysis over IndicatorSnapshot: CALL setups blocked at
        // RSI 30-40 (dip in an already-bullish 5m trend) and PUT setups blocked at RSI 60-70
        // (bounce in an already-bearish 5m trend) showed a positive forward-move edge across
        // nearly every trading day in the sample.
        var enableRsiPullbackFilter = _strategySettings.GetBool("EnableRsiPullbackFilter", symbol, false);
        var rsiCallPullbackFloor    = _strategySettings.GetDecimal("RsiCallPullbackFloor", symbol, 30m);
        var rsiCallPullbackCeiling  = _strategySettings.GetDecimal("RsiCallPullbackCeiling", symbol, 40m);
        var rsiPutBouncebackFloor   = _strategySettings.GetDecimal("RsiPutBouncebackFloor", symbol, 60m);
        var rsiPutBouncebackCeiling = _strategySettings.GetDecimal("RsiPutBouncebackCeiling", symbol, 70m);

        // Per-filter enable/disable flags — DB-driven so individual filters can be
        // switched off for testing without touching trading logic. All default to true,
        // matching current behavior when no override row exists.
        var enableTrendFilter       = _strategySettings.GetBool("EnableTrendFilter", symbol, true);
        var enablePcrFilter         = _strategySettings.GetBool("EnablePcrFilter", symbol, true);
        var enableRsiFilter         = _strategySettings.GetBool("EnableRsiFilter", symbol, true);
        var enableDeltaFilter       = _strategySettings.GetBool("EnableDeltaFilter", symbol, true);
        var enablePriceChangeFilter = _strategySettings.GetBool("EnablePriceChangeFilter", symbol, true);
        var enableCandleFilter      = _strategySettings.GetBool("EnableCandleFilter", symbol, true);

        var pcrBullishThreshold = _strategySettings.GetDecimal("PcrBullishThreshold", symbol, 1.00m);
        var pcrBearishThreshold = _strategySettings.GetDecimal("PcrBearishThreshold", symbol, 1.00m);

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

        if (enableSignalScoring)
        {
            return GenerateScored(
                symbol, ltp, ema20, ema50, pcr, rsi, currentVolume, avgVolume20, priceChangePct, atrPct, atrRaw,
                chain, atmStrike, atmCe, atmPe, trendBullish, trendBearish, lastClosedCandle, bodyPct,
                rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling, bodyPctMinimum,
                pcrBullishThreshold, pcrBearishThreshold);
        }

        // ── Legacy path — unchanged AND-combined CALL/PUT conditions ─────────────
        // Candle body confirmation — filters weak/indecisive closed candles (1m or 5m,
        // whichever candle the caller passed as lastClosedCandle — see StrategyManager's
        // UseOneMinuteEntryCandle setting).
        // Body must be at least bodyPctMinimum of the total range in the direction of the trade.
        bool candleBullish =
         lastClosedCandle.Close > lastClosedCandle.Open &&
         bodyPct >= bodyPctMinimum;

        bool candleBearish =
            lastClosedCandle.Close < lastClosedCandle.Open &&
            bodyPct >= bodyPctMinimum;
        // ── Entry: 1m timing + 5m trend alignment + candle strength ──────────
        // Each sub-condition is named below (same operands/thresholds, just factored out of the
        // single boolean expression) purely so a rejection can report exactly which ones failed —
        // the values of `call`/`put` themselves are unchanged.
        bool condTrendBullish = !enableTrendFilter || trendBullish;
        bool condLtpAboveEma20 = !enableTrendFilter || ltp > ema20;
        bool condEma20AboveEma50 = !enableTrendFilter || ema20 > ema50;
        bool condRsiCall = !enableRsiFilter
            || (rsi >= rsiCallFloor && rsi <= rsiCallCeiling)
            || (enableRsiPullbackFilter && rsi >= rsiCallPullbackFloor && rsi <= rsiCallPullbackCeiling);
        bool condDeltaCall = !enableDeltaFilter || ceAtmDelta >= deltaMinimum;
        bool condPriceChangeCall = !enablePriceChangeFilter || priceChangePct > priceChangeThreshold;
        bool condCandleBullish = !enableCandleFilter || candleBullish;
        bool condPcrCall = !enablePcrFilter || pcr >= pcrBullishThreshold;

        bool call =
     condTrendBullish &&
     condLtpAboveEma20 &&
     condEma20AboveEma50 &&
     condPcrCall &&
     condRsiCall &&
     condDeltaCall &&
     condPriceChangeCall &&
     condCandleBullish;

        bool condTrendBearish = !enableTrendFilter || trendBearish;
        bool condLtpBelowEma20 = !enableTrendFilter || ltp < ema20;
        bool condEma20BelowEma50 = !enableTrendFilter || ema20 < ema50;
        bool condPcrPut = !enablePcrFilter || pcr <= pcrBearishThreshold;
        bool condRsiPut = !enableRsiFilter
            || (rsi >= rsiPutFloor && rsi <= rsiPutCeiling)
            || (enableRsiPullbackFilter && rsi >= rsiPutBouncebackFloor && rsi <= rsiPutBouncebackCeiling);
        bool condDeltaPut = !enableDeltaFilter || peAtmDelta <= -deltaMinimum;
        bool condPriceChangePut = !enablePriceChangeFilter || priceChangePct < -priceChangeThreshold;
        bool condCandleBearish = !enableCandleFilter || candleBearish;

        bool put =
            condTrendBearish &&
            condLtpBelowEma20 &&
            condEma20BelowEma50 &&
            condPcrPut &&
            condRsiPut &&
            condDeltaPut &&
            condPriceChangePut &&
            condCandleBearish;

        if (!call && !put)
        {
            var callChecks = new (string Label, bool Passed)[]
            {
                ("Trend Bullish", condTrendBullish),
                ("LTP > EMA20", condLtpAboveEma20),
                ("EMA20 > EMA50", condEma20AboveEma50),
                ($"PCR >= {pcrBullishThreshold}", condPcrCall),
                (enableRsiPullbackFilter
                    ? $"RSI in ({rsiCallFloor},{rsiCallCeiling}) or ({rsiCallPullbackFloor},{rsiCallPullbackCeiling})"
                    : $"RSI in ({rsiCallFloor},{rsiCallCeiling})", condRsiCall),
                ($"CE Delta >= {deltaMinimum}", condDeltaCall),
                ($"PriceChangePct > {priceChangeThreshold}", condPriceChangeCall),
                ("Candle bullish", condCandleBullish),
            };
            var putChecks = new (string Label, bool Passed)[]
            {
                ("Trend Bearish", condTrendBearish),
                ("LTP < EMA20", condLtpBelowEma20),
                ("EMA20 < EMA50", condEma20BelowEma50),
                ($"PCR <= {pcrBearishThreshold}", condPcrPut),
                (enableRsiPullbackFilter
                    ? $"RSI in ({rsiPutFloor},{rsiPutCeiling}) or ({rsiPutBouncebackFloor},{rsiPutBouncebackCeiling})"
                    : $"RSI in ({rsiPutFloor},{rsiPutCeiling})", condRsiPut),
                ($"PE Delta <= -{deltaMinimum}", condDeltaPut),
                ($"PriceChangePct < -{priceChangeThreshold}", condPriceChangePut),
                ("Candle bearish", condCandleBearish),
            };

            var failureReasons = callChecks.Where(c => !c.Passed).Select(c => $"CALL: {c.Label}")
                .Concat(putChecks.Where(c => !c.Passed).Select(c => $"PUT: {c.Label}"))
                .ToList();

            return SignalGenerationResult.Failed(failureReasons);
        }

        // NOTE: parameter order here previously mismatched ComputeConfidence's signature (bodyPct,
        // rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling, bodyPctMinimum -- bodyPctMinimum
        // is LAST) -- bodyPctMinimum was landing in the rsiCallFloor slot and every RSI threshold
        // was shifted one position, silently scrambling every confidence score this method ever
        // produced (including the live lot-sizing tiers and the 70%-confidence gate). Fixed below;
        // flagged separately since it's a pre-existing defect, not part of the scoring-engine change.
        var confidence = ComputeConfidence(
    call,
    ema20,
    ema50,
    ltp,
    pcr,
    rsi,
    currentVolume,
    avgVolume20,
    bodyPct,
    rsiCallFloor,
    rsiCallCeiling,
    rsiPutFloor,
    rsiPutCeiling,
    bodyPctMinimum);

        var legacyOptionType = call ? TradingConst.OptionType.CE : TradingConst.OptionType.PE;
        return BuildSignalResult(symbol, ltp, atrRaw, chain, atmStrike, legacyOptionType, confidence, call, null);
    }

    // ── Scored path — CALL and PUT evaluated independently, strongest eligible candidate wins ──
    private SignalGenerationResult GenerateScored(
        string symbol, decimal ltp, decimal ema20, decimal ema50, decimal pcr, decimal rsi,
        decimal currentVolume, decimal avgVolume20, decimal priceChangePct, decimal atrPct, decimal atrRaw,
        List<OptionChainData> chain, decimal atmStrike, OptionChainData atmCe, OptionChainData atmPe,
        bool trendBullish, bool trendBearish, Candle lastClosedCandle, decimal bodyPct,
        decimal rsiCallFloor, decimal rsiCallCeiling, decimal rsiPutFloor, decimal rsiPutCeiling,
        decimal bodyPctMinimum, decimal pcrBullishThreshold, decimal pcrBearishThreshold)
    {
        var (callScore, putScore, callConfidence, putConfidence) = ComputeScores(
            symbol, ltp, ema20, ema50, pcr, rsi, currentVolume, avgVolume20, priceChangePct, atrPct,
            atmCe, atmPe, trendBullish, trendBearish, lastClosedCandle, bodyPct,
            rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling, bodyPctMinimum,
            pcrBullishThreshold, pcrBearishThreshold);

        var scoreThreshold = _strategySettings.GetDecimal("SignalScoreThreshold", symbol, 10m);
        bool callEligible = callScore.Total >= scoreThreshold;
        bool putEligible = putScore.Total >= scoreThreshold;

        if (!callEligible && !putEligible)
        {
            var best = callScore.Total >= putScore.Total ? callScore : putScore;
            return SignalGenerationResult.Failed(
                new[]
                {
                    $"CALL score={callScore.Total} Required={scoreThreshold:F0} ({callScore})",
                    $"PUT score={putScore.Total} Required={scoreThreshold:F0} ({putScore})",
                },
                best);
        }

        bool chooseCall;
        if (callEligible && putEligible)
        {
            if (callScore.Total != putScore.Total)
                chooseCall = callScore.Total > putScore.Total;
            else
                // Tie on score — StrategySelectorService has no CALL/PUT tie-break of its own
                // (it only arbitrates Directional vs HeroZero vs Expiry), so fall back to the one
                // existing per-direction quality signal already computed here: ComputeConfidence.
                chooseCall = callConfidence >= putConfidence;
        }
        else
        {
            chooseCall = callEligible;
        }

        var winningScore = chooseCall ? callScore : putScore;
        var confidence = chooseCall ? callConfidence : putConfidence;
        var optionType = chooseCall ? TradingConst.OptionType.CE : TradingConst.OptionType.PE;

        return BuildSignalResult(symbol, ltp, atrRaw, chain, atmStrike, optionType, confidence, chooseCall, winningScore);
    }

    // ── Independent CALL/PUT score computation — shared by GenerateScored and the internal test
    // seam below, so tests can verify both candidates' breakdowns without duplicating this logic. ──
    private (SignalScore Call, SignalScore Put, decimal CallConfidence, decimal PutConfidence) ComputeScores(
        string symbol, decimal ltp, decimal ema20, decimal ema50, decimal pcr, decimal rsi,
        decimal currentVolume, decimal avgVolume20, decimal priceChangePct, decimal atrPct,
        OptionChainData atmCe, OptionChainData atmPe, bool trendBullish, bool trendBearish,
        Candle lastClosedCandle, decimal bodyPct,
        decimal rsiCallFloor, decimal rsiCallCeiling, decimal rsiPutFloor, decimal rsiPutCeiling,
        decimal bodyPctMinimum, decimal pcrBullishThreshold, decimal pcrBearishThreshold)
    {
        var bodyPctScoreLowThreshold = _strategySettings.GetDecimal("BodyPctScoreLowThreshold", symbol, 20m);
        var priceChangeScoreThreshold = _strategySettings.GetDecimal("PriceChangeScoreThreshold", symbol, 0.15m);

        // ATR quality bands — the top band reuses MinAtrPercent directly (the same bar that used
        // to be a hard gate), so switching EnableSignalScoring on doesn't silently change what
        // counts as good ATR, only what happens when it isn't met. No separate "high threshold"
        // setting — a distinct key here would just default straight back to MinAtrPercent's value.
        var atrScoreHighThreshold = _strategySettings.GetDecimal("MinAtrPercent", symbol, 0.10m);
        var atrScoreLowThreshold = _strategySettings.GetDecimal(
            "AtrScoreLowThreshold", symbol, Math.Max(0m, atrScoreHighThreshold - 0.005m));

        var callConfidence = ComputeConfidence(true, ema20, ema50, ltp, pcr, rsi, currentVolume, avgVolume20,
            bodyPct, rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling, bodyPctMinimum);
        var putConfidence = ComputeConfidence(false, ema20, ema50, ltp, pcr, rsi, currentVolume, avgVolume20,
            bodyPct, rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling, bodyPctMinimum);

        var candleDelta = lastClosedCandle.Close - lastClosedCandle.Open;

        var callScore = new SignalScore
        {
            IsCall = true,
            Confidence = callConfidence,
            TrendScore = trendBullish ? 2 : 0,
            Ema20Score = ltp > ema20 ? 2 : 0,
            Ema50Score = ema20 > ema50 ? 2 : 0,
            RsiScore = (rsi >= rsiCallFloor && rsi <= rsiCallCeiling) ? 2 : 0,
            PriceChangeScore = priceChangePct > priceChangeScoreThreshold ? 1 : 0,
            CandleScore = candleDelta > 0 ? 1 : 0,
            BodyScore = bodyPct >= bodyPctMinimum ? 2 : bodyPct >= bodyPctScoreLowThreshold ? 1 : 0,
            AtrScore = atrPct >= atrScoreHighThreshold ? 2 : atrPct >= atrScoreLowThreshold ? 1 : 0,
            PcrScore = pcr >= pcrBullishThreshold ? 1 : 0,
            OiScore = (_oi.IsLongBuildUp(atmCe.ChangeInOi, candleDelta) || _oi.IsShortCovering(atmCe.ChangeInOi, candleDelta)) ? 1 : 0,
        };
        callScore.ConfidenceScore = callConfidence >= 70m ? 3 : callConfidence >= 65m ? 2 : callConfidence >= 60m ? 1 : 0;

        var putScore = new SignalScore
        {
            IsCall = false,
            Confidence = putConfidence,
            TrendScore = trendBearish ? 2 : 0,
            Ema20Score = ltp < ema20 ? 2 : 0,
            Ema50Score = ema20 < ema50 ? 2 : 0,
            RsiScore = (rsi >= rsiPutFloor && rsi <= rsiPutCeiling) ? 2 : 0,
            PriceChangeScore = priceChangePct < -priceChangeScoreThreshold ? 1 : 0,
            CandleScore = candleDelta < 0 ? 1 : 0,
            BodyScore = bodyPct >= bodyPctMinimum ? 2 : bodyPct >= bodyPctScoreLowThreshold ? 1 : 0,
            AtrScore = atrPct >= atrScoreHighThreshold ? 2 : atrPct >= atrScoreLowThreshold ? 1 : 0,
            PcrScore = pcr <= pcrBearishThreshold ? 1 : 0,
            OiScore = (_oi.IsLongBuildUp(atmPe.ChangeInOi, -candleDelta) || _oi.IsShortCovering(atmPe.ChangeInOi, -candleDelta)) ? 1 : 0,
        };
        putScore.ConfidenceScore = putConfidence >= 70m ? 3 : putConfidence >= 65m ? 2 : putConfidence >= 60m ? 1 : 0;

        return (callScore, putScore, callConfidence, putConfidence);
    }

    // Test-only seam (see AssemblyInfo InternalsVisibleTo -> Trading.Tests). Exposes both
    // independently computed candidate scores without changing any public behavior, so tests can
    // verify the CALL/PUT comparison and tie-break rule (spec: "CALL score > PUT score -> CALL")
    // directly instead of only observing GenerateScored's single winner.
    internal (SignalScore Call, SignalScore Put) ComputeScoresForTest(
        string symbol, decimal ltp, decimal ema20, decimal ema50, decimal ema20_5m, decimal ema50_5m,
        decimal pcr, decimal rsi, decimal currentVolume, decimal avgVolume20, decimal priceChangePct,
        decimal atrPct, Candle lastClosedCandle, decimal bodyPct, OptionChainData atmCe, OptionChainData atmPe,
        decimal rsiCallFloor = 55m, decimal rsiCallCeiling = 70m, decimal rsiPutFloor = 30m, decimal rsiPutCeiling = 45m,
        decimal bodyPctMinimum = 35m, decimal pcrBullishThreshold = 1.00m, decimal pcrBearishThreshold = 1.00m)
    {
        bool trendBullish = ema20_5m > 0 && ema50_5m > 0 && ema20_5m > ema50_5m;
        bool trendBearish = ema20_5m > 0 && ema50_5m > 0 && ema20_5m < ema50_5m;

        var (call, put, _, _) = ComputeScores(
            symbol, ltp, ema20, ema50, pcr, rsi, currentVolume, avgVolume20, priceChangePct, atrPct,
            atmCe, atmPe, trendBullish, trendBearish, lastClosedCandle, bodyPct,
            rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling, bodyPctMinimum,
            pcrBullishThreshold, pcrBearishThreshold);
        return (call, put);
    }

    // ── Shared tail — option-leg lookup, index/option SL/target math, SignalBuilder ─────────────
    // Identical for both the legacy and scored paths once `call`/`confidence` are known; factored
    // out so the two decision paths above don't duplicate ~80 lines of risk/target construction.
    private SignalGenerationResult BuildSignalResult(
        string symbol, decimal ltp, decimal atrRaw, List<OptionChainData> chain, decimal atmStrike,
        string optionType, decimal confidence, bool call, SignalScore? score)
    {
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
            return SignalGenerationResult.Failed(new[] { "Stale/zero option premium for selected leg" }, score);

        // Prefer Groww's TradingSymbol; fall back to constructed name
        var optionSymbol = (chainLeg != null && !string.IsNullOrWhiteSpace(chainLeg.TradingSymbol))
            ? chainLeg.TradingSymbol
            : $"{symbol}{IstClock.Now:yyMMM}{(int)atmStrike}{optionType}".ToUpper();

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

        // ===============================
        // OPTION Targets — option's OWN candles (ATR + support/resistance) first;
        // falls back to a delta-adjusted index-based estimate only when there isn't
        // enough option-candle history yet (e.g. just after open, or a strike picked
        // for the first time today).
        // ===============================

        decimal optionSL;
        decimal optionT1;
        decimal optionT2;

        var delta = Math.Abs(chainLeg!.Delta);
        if (delta <= 0)
            delta = 0.45m;

        var stopDistance = Math.Abs(ltp - sl);

        var optionRisk = stopDistance * delta;
        var optionTarget1 = atrRaw * Settings.AtrTarget1Multiplier * delta;
        var optionTarget2 = atrRaw * Settings.AtrTarget2Multiplier * delta;

        optionSL = Math.Round(optionLtp - optionRisk, 2);

        var maxLoss = optionLtp * 0.80m;
        if (optionSL < maxLoss)
            optionSL = maxLoss;

        optionT1 = Math.Round(optionLtp + optionTarget1, 2);
        optionT2 = Math.Round(optionLtp + optionTarget2, 2);
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


        return SignalGenerationResult.Success(builder.Build(), score);
    }

    private static string FormatChecklist(IEnumerable<(string Label, bool Passed)> items) =>
        string.Join(Environment.NewLine, items.Select(i => $"{(i.Passed ? "✓" : "✗")} {i.Label}"));
}
