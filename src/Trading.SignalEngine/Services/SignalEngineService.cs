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
    decimal bodyPctMinimum,
    bool rsiActive)
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

        // RSI. IsActive = 0 on the relevant floor/ceiling pair means this contribution does not
        // apply at all — it is NOT the same as "fall back to the compiled default and keep
        // scoring against it". Only BodyPctMinimum (the candle-quality bonus below) keeps the
        // fall-back-to-default behaviour; every other decimal setting either gates a hard
        // AND-condition or, here, a scoring contribution, and both skip entirely when inactive.
        if (rsiActive)
        {
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
        }

        // Volume scoring intentionally removed. currentVolume/avgVolume20 are still accepted as
        // parameters (callers and signatures unchanged) but contribute nothing to the score.
        // Groww publishes no index candle volume on this data path -- every Candle row is
        // Volume=0 -- so currentVolume was always 0 and the old `if (currentVolume > 0 ...)`
        // bonus could never fire. Deleting the branch is therefore behaviour-neutral; it just
        // stops the formula implying a volume input it never actually had.
        // Practical effect on the scale: max attainable score is 85, not 99
        // (25 base + 20 EMA + 15 PCR + 15 RSI + 10 candle). Relevant when reading the
        // hardcoded 70 confidence floor in StrategyManager.

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

    /// <summary>
    /// The RSI band ComputeConfidence scores against. It is the entry-gate band (condition 5 of
    /// CallEntryEvaluator / PutEntryEvaluator), read with the same compiled defaults, so each side
    /// has exactly one RSI band. Both signal paths use this, so they cannot drift apart.
    /// </summary>
    private (decimal CallFloor, decimal CallCeiling, decimal PutFloor, decimal PutCeiling) RsiConfidenceBand(string symbol) =>
        (_strategySettings.GetDecimal("CallRsiFloor", symbol, 60m),
         _strategySettings.GetDecimal("CallRsiCeiling", symbol, 70m),
         _strategySettings.GetDecimal("PutRsiFloor", symbol, 28m),
         _strategySettings.GetDecimal("PutRsiCeiling", symbol, 40m));

    /// <summary>
    /// IsActive = 0 on either half of the side's band means the RSI confidence contribution does
    /// not apply - see the comment inside ComputeConfidence. Deliberately NOT tied to
    /// EnableRsiFilter: that flag switches the PUT entry condition, not the confidence score.
    /// </summary>
    private bool IsRsiConfidenceActive(string symbol, bool call) =>
        call
            ? _strategySettings.IsSettingActive("CallRsiFloor", symbol) && _strategySettings.IsSettingActive("CallRsiCeiling", symbol)
            : _strategySettings.IsSettingActive("PutRsiFloor", symbol) && _strategySettings.IsSettingActive("PutRsiCeiling", symbol);

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
        Candle? lastClosedCandle = null,
        decimal? rsi5m = null)
        => GenerateSignalDetailed(
            symbol, ltp, ema20, ema50, pcr, rsi, currentVolume, avgVolume20,
            priceChangePct, atrPct, atrRaw, optionChain, strategyHint,
            ema20_5m, ema50_5m, lastClosedCandle, rsi5m: rsi5m).Signal;

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
        Candle? lastClosedCandle = null,
        MarketState marketState = MarketState.Trend,
        // ── CALL day-filter / breakout inputs ────────────────────────────────────────────────
        // 0 means "not supplied", and the conditions built on these FAIL CLOSED: a missing input
        // is treated as not-confirmed, so no CALL is generated. That is deliberate — on a live
        // account "unknown" must never read as "confirmed" — but it does mean a caller that omits
        // these arguments gets no CALL signals at all. Every production path (StrategyManager)
        // supplies them from MarketSnapshot.
        decimal vwap = 0,
        decimal prev5mHigh = 0,
        decimal prev5mLow = 0,
        decimal dayOpen = 0,
        decimal dayLow = 0,
        decimal dayHigh = 0,
        decimal dayChangePct = 0,
        // Entry RSI: 5-minute RSI(14) on closed 5m bars (MarketSnapshot.Rsi5m). Null means fewer
        // than 15 closed 5m bars exist and the CALL/PUT RSI condition FAILS CLOSED - null is never
        // read as 50. `rsi` (1m) is kept for confidence scoring only.
        decimal? rsi5m = null,
        // Previous session close, used ONLY by the opening-conditional RSI band (condition 5).
        // 0 = unknown, which disables that override and leaves the normal band in force.
        decimal yesterdayClose = 0)
    {
        // No chain data = no delta confirmation = no signal.
        if (optionChain == null || optionChain.Count == 0)
            return SignalGenerationResult.Failed("No option chain data available");

        // ATR% filter — suppress signals in low-volatility/sideways markets.
        // Threshold is instrument-specific since indices differ in volatility.
        atrPct = ltp > 0
    ? atrRaw / ltp * 100m
    : 0m;

        var minAtr = _strategySettings.GetDecimal("MinAtrPercent", symbol, 0.030000m);

        // IsActive = 0 on MinAtrPercent means this check does not apply at all - it must not
        // silently fall back to the compiled default and keep enforcing it.
        if (_strategySettings.IsSettingActive("MinAtrPercent", symbol) && atrPct < minAtr)
            return SignalGenerationResult.Failed($"ATR% {atrPct:F2} below minimum {minAtr:F2}");

        if (lastClosedCandle == null)
            return SignalGenerationResult.Failed("No closed candle");
        var range = lastClosedCandle.High - lastClosedCandle.Low;
        if (range <= 0)
            return SignalGenerationResult.Failed("Invalid candle range");
        var body = Math.Abs(lastClosedCandle.Close - lastClosedCandle.Open);
        var bodyPct = body / range * 100m;

        // CONFIDENCE-ONLY settings. The RSI band is the SAME Call/PutRsiFloor/Ceiling pair the
        // entry chains gate on (condition 5), with the same compiled defaults - one band per side,
        // so tuning RSI can no longer hit a look-alike key that only moves lot sizing. The legacy
        // RsiCallFloor/Ceiling and RsiPutFloor/Ceiling keys are no longer read anywhere.
        // BodyPctMinimum is still the legacy candle-quality key and is intentionally unchanged.
        var bodyPctMinimum = _strategySettings.GetDecimal("BodyPctMinimum", symbol, 30m);
        var (rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling) = RsiConfidenceBand(symbol);

        // -- ATM strike + priced legs (the volume/OI/premium liquidity filter was removed; only Ltp > 0 is required) ------------------------------------------------------------
        var interval = (decimal)_instrumentCache.GetStrikeInterval(symbol);
        var spot = optionChain
            .Where(x => x.UnderlyingLtp > 0)
            .Select(x => x.UnderlyingLtp)
            .FirstOrDefault();

        if (spot <= 0)
            spot = ltp;

        var atmStrike = Math.Round(spot / interval, MidpointRounding.AwayFromZero) * interval;

        var chain = optionChain!;
        var atmCe = chain
            .Where(x => x.OptionType == TradingConst.OptionType.CE && x.Ltp > 0)
            .OrderBy(x => Math.Abs(x.StrikePrice - atmStrike))
            .ThenByDescending(x => x.Volume)
            .ThenByDescending(x => x.OpenInterest)
            .FirstOrDefault();

        var atmPe = chain
            .Where(x => x.OptionType == TradingConst.OptionType.PE && x.Ltp > 0)
            .OrderBy(x => Math.Abs(x.StrikePrice - atmStrike))
            .ThenByDescending(x => x.Volume)
            .ThenByDescending(x => x.OpenInterest)
            .FirstOrDefault();

        if (atmCe == null || atmPe == null)
            return SignalGenerationResult.Failed("No priced ATM CE/PE strikes found");

        var ceAtmDelta = atmCe.Delta;
        var peAtmDelta = atmPe.Delta;

        // 5m trend alignment. Retired from both entry chains (EnableCall5mTrendFilter /
        // EnablePut5mTrendFilter, both default off) but still computed and passed through, so
        // restoring either flag needs no further plumbing.
        bool trendBullish = ema20_5m > 0 && ema50_5m > 0 && ema20_5m > ema50_5m;
        bool trendBearish = ema20_5m > 0 && ema50_5m > 0 && ema20_5m < ema50_5m;

        // ── Direction evaluation ─────────────────────────────────────────────────────────────
        // CALL and PUT are each a self-contained 12-line AND-chain with its own settings, its own
        // thresholds and its own per-condition labels. They were previously inlined here, which
        // made this method ~450 lines and put the two specifications far enough apart that a
        // condition could be dropped from one expression while its failure label stayed behind —
        // which is exactly the StrongTrend defect that misreported live blocks. Each side now
        // returns a DirectionVerdict carrying BOTH the pass/fail and the labelled checks, so the
        // expression and its explanation cannot drift apart.
        var inputs = new EntryMarketInputs(
            Ltp: ltp, Ema20: ema20, Ema50: ema50, Rsi: rsi, Rsi5m: rsi5m, Pcr: pcr, AtrPct: atrPct,
            PriceChangePct: priceChangePct,
            CurrentVolume: currentVolume, AvgVolume20: avgVolume20,
            Vwap: vwap, Prev5mHigh: prev5mHigh, Prev5mLow: prev5mLow,
            DayOpen: dayOpen, DayLow: dayLow, DayHigh: dayHigh, DayChangePct: dayChangePct,
            YesterdayClose: yesterdayClose,
            CeAtmDelta: ceAtmDelta, PeAtmDelta: peAtmDelta,
            CeChangeInOi: atmCe.ChangeInOi, PeChangeInOi: atmPe.ChangeInOi,
            TrendBullish: trendBullish, TrendBearish: trendBearish,
            LastClosedCandle: lastClosedCandle);

        var callVerdict = EvaluateCall(symbol, inputs);
        var putVerdict = EvaluatePut(symbol, inputs);

        if (!callVerdict.Passed && !putVerdict.Passed)
        {
            var failureReasons = callVerdict.FailedLabels("CALL")
                .Concat(putVerdict.FailedLabels("PUT"))
                .ToList();

            return SignalGenerationResult.Failed(failureReasons);
        }

        // ── Select ONE direction ─────────────────────────────────────────────────────────────
        // The two sides are structurally exclusive whenever EnableTrendFilter is on, since
        // condition 1 is EMA20 > EMA50 for CALL and EMA20 < EMA50 for PUT. With that filter
        // disabled both can pass, and CALL wins — the same precedence the previous
        // `call ? CE : PE` expression had. Made explicit rather than implied by operator order.
        var call = callVerdict.Passed;

        // IsActive=0 on either half of the relevant RSI band means the RSI confidence
        // contribution does not apply - see the comment inside ComputeConfidence.
        var rsiConfidenceActive = IsRsiConfidenceActive(symbol, call);

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
    bodyPctMinimum,
    rsiConfidenceActive);

        var optionType = call ? TradingConst.OptionType.CE : TradingConst.OptionType.PE;
        return BuildSignalResult(symbol, ltp, atrRaw, chain, atmStrike, optionType, confidence, call);
    }

    // -- Direction evaluation ---------------------------------------------------------------

    /// <summary>
    /// Market inputs both direction evaluators read. Settings are NOT carried here - each
    /// evaluator resolves its own from IStrategySettingsService (an in-memory cache), so a
    /// direction's specification lives in exactly one place.
    /// </summary>
    /// <summary>
    /// The RSI band for this session, after any opening-conditional override.
    ///
    /// The live band is one pair for every session, but a gap-up session runs at a higher RSI than a
    /// flat one - on the stored history RSI >= 70 was the strongest 30-minute up-drift (+0.53 ATR,
    /// 59% up) and the CALL ceiling of 65 excludes it. These settings let each opening carry its own
    /// band. ALL OFF BY DEFAULT: with EnableOpeningRsiBand = 0, or no override rows, this returns the
    /// normal band unchanged and the entry chain behaves exactly as before.
    ///
    ///   EnableOpeningRsiBand   0/1                                   (default 0)
    ///   OpeningRsiGapPct       gap % separating the openings          (default 0.30)
    ///   GapUpCallRsiFloor / GapUpCallRsiCeiling     CALL on a gap-up session
    ///   GapUpPutRsiFloor  / GapUpPutRsiCeiling      PUT  on a gap-up session
    ///   GapDownCallRsiFloor / GapDownCallRsiCeiling CALL on a gap-down session
    ///   GapDownPutRsiFloor  / GapDownPutRsiCeiling  PUT  on a gap-down session
    ///
    /// An override applies only when BOTH its floor and ceiling rows exist and are active, so a half
    /// configured pair can never silently widen one end of the band.
    /// </summary>
    private (decimal Floor, decimal Ceiling) OpeningRsiBand(
        string symbol, bool call, decimal floor, decimal ceiling, in EntryMarketInputs m)
    {
        if (!_strategySettings.GetBool("EnableOpeningRsiBand", symbol, false))
            return (floor, ceiling);
        if (m.DayOpen <= 0 || m.YesterdayClose <= 0)
            return (floor, ceiling);

        var gapPct = (m.DayOpen - m.YesterdayClose) / m.YesterdayClose * 100m;
        var threshold = _strategySettings.GetDecimal("OpeningRsiGapPct", symbol, 0.30m);

        var prefix = gapPct >= threshold ? "GapUp"
                   : gapPct <= -threshold ? "GapDown"
                   : null;
        if (prefix is null) return (floor, ceiling);      // normal open keeps the live band

        var floorKey = $"{prefix}{(call ? "Call" : "Put")}RsiFloor";
        var ceilingKey = $"{prefix}{(call ? "Call" : "Put")}RsiCeiling";
        if (!_strategySettings.IsSettingActive(floorKey, symbol)
            || !_strategySettings.IsSettingActive(ceilingKey, symbol))
            return (floor, ceiling);

        return (_strategySettings.GetDecimal(floorKey, symbol, floor),
                _strategySettings.GetDecimal(ceilingKey, symbol, ceiling));
    }

    /// <summary>
    /// True when the session opened at least OpeningRsiGapPct (default 0.30%) above the previous close - the same gap definition
    /// OpeningRsiBand uses. False when DayOpen or YesterdayClose is missing, so an unclassifiable gap never skips a filter.
    /// </summary>
    private bool IsGapUpSession(string symbol, in EntryMarketInputs m)
    {
        if (m.DayOpen <= 0 || m.YesterdayClose <= 0) return false;
        var gapPct = (m.DayOpen - m.YesterdayClose) / m.YesterdayClose * 100m;
        return gapPct >= _strategySettings.GetDecimal("OpeningRsiGapPct", symbol, 0.30m);
    }

    internal readonly record struct EntryMarketInputs(
        decimal Ltp,
        decimal Ema20,
        decimal Ema50,
        decimal Rsi,
        decimal? Rsi5m,
        decimal Pcr,
        decimal AtrPct,
        decimal PriceChangePct,
        decimal CurrentVolume,
        decimal AvgVolume20,
        decimal Vwap,
        decimal Prev5mHigh,
        decimal Prev5mLow,
        decimal DayOpen,
        decimal DayLow,
        decimal DayHigh,
        decimal DayChangePct,
        /// <summary>Previous session close, for the opening-gap classification in condition 5.</summary>
        decimal YesterdayClose,
        decimal CeAtmDelta,
        decimal PeAtmDelta,
        long CeChangeInOi,
        long PeChangeInOi,
        bool TrendBullish,
        bool TrendBearish,
        Candle? LastClosedCandle);

    /// <summary>
    /// One direction's verdict: whether it fired, and the labelled state of EVERY condition that
    /// decided it.
    ///
    /// Passed is DERIVED from Checks rather than stored alongside it, so a condition cannot be
    /// enforced without appearing in the failure list, nor listed without being enforced. That
    /// coupling is the whole point: the previous inline version reported "MarketState is
    /// StrongTrend" as a CALL blocker while that condition was commented out of the expression,
    /// and a live diagnosis was misread as a result.
    /// </summary>
    internal readonly record struct DirectionVerdict((string Label, bool Passed)[] Checks)
    {
        public bool Passed => Checks.All(c => c.Passed);

        public IEnumerable<string> FailedLabels(string side) =>
            Checks.Where(c => !c.Passed).Select(c => $"{side}: {c.Label}");
    }

    /// <summary>
    /// CALL entry specification - up to 11 conditions, each appended to the list only when its
    /// Enable* flag (if any) is on AND every threshold it reads is IsSettingActive. A condition
    /// that does not apply is simply absent from DirectionVerdict.Checks - not present-and-true -
    /// so a checklist read off a live signal only ever shows conditions that were actually
    /// evaluated.
    ///
    ///   1 EMA20 &gt; EMA50             7 Body% >= CallBodyPctMinimum
    ///   2 LTP &gt; EMA20               8 Volume &gt; VolumeSMA20  (NOT WIRED - no volume data)
    ///   3 LTP &gt; VWAP  (NOT WIRED - no index volume, so no VWAP)
    ///   4 LTP &gt; Prev 5m High        9 ATR% >= CallMinAtrPercent
    ///   5 RSI CallRsiFloor..Ceiling  10 CE Delta in [CallDeltaMinimum(0.35), CallDeltaMaximum(0.65)]
    ///   6 Bullish candle            11 DayChange% > min OR (Recovery% > min AND LTP &gt; DayOpen)
    ///                               12 PCR >= CallPcrMinimum (off by default - see condition 12)
    ///
    /// CONDITIONS 3 AND 8 ARE NOT WIRED BECAUSE THEIR INPUTS DO NOT EXIST. Groww publishes no
    /// index candle volume, so Vwap is NULL on every row (verified 2026-09-02: 328,092 of
    /// 328,092) and every Candle.Volume is 0. Wiring them live would suppress every CALL forever -
    /// they need a volume-bearing feed to mean anything.
    ///
    /// TWO PREVIOUS CONDITIONS ARE RETIRED behind flags rather than deleted: the 5m EMA trend
    /// alignment, and PriceChangePct measured against today's OPEN (superseded by 11, which
    /// measures against YESTERDAY'S CLOSE and adds the recovery-from-low path).
    ///
    /// Missing inputs FAIL CLOSED - a 0 prev5mHigh or dayLow yields no CALL, because "unknown"
    /// must never read as "confirmed" on a live account.
    /// </summary>
    internal DirectionVerdict EvaluateCall(string symbol, in EntryMarketInputs m)
    {
        var conditions = new List<(string Description, bool Passed)>();

        bool Active(string key) =>
            _strategySettings.IsSettingActive(key, symbol);

        bool Enabled(string key, bool defaultValue = true) =>
            _strategySettings.GetBool(key, symbol, defaultValue);

        decimal Threshold(string key, decimal defaultValue) =>
            _strategySettings.GetDecimal(key, symbol, defaultValue);

        // 1 + 2: Trend
        if (Enabled("EnableTrendFilter"))
        {
            conditions.Add((
                $"1 EMA20 > EMA50 ({m.Ema20:F2} vs {m.Ema50:F2})",
                m.Ema20 > m.Ema50));

            conditions.Add((
                $"2 LTP > EMA20 ({m.Ltp:F2} vs {m.Ema20:F2})",
                m.Ltp > m.Ema20));
        }

        // 2b: EMA gap (shared CALL/PUT condition, off by default - see EmaGapCondition).
        if (EmaGapCondition.Evaluate(m.Ema20, m.Ema50, Enabled, Active, Threshold) is { } emaGap)
            conditions.Add(emaGap);

        // 3 - LTP > VWAP. Not wired: Groww publishes no index candle volume, so Vwap is NULL on
        // every row (verified 2026-09-02: 328,092 of 328,092) and every Candle.Volume is 0.
        // Wiring this live would suppress every CALL forever - it needs a volume-bearing feed.

        // 4: Previous five-minute high
        if (Enabled("EnableCallPrev5mHighFilter"))
        {
            const decimal buffer = 5m; // Compare 5 vs 10 in replay

            conditions.Add((
                $"4 LTP > Prev5mHigh + {buffer} ({(m.Prev5mHigh > 0 ? m.Prev5mHigh.ToString("F2") : "unavailable")})",
                m.Prev5mHigh > 0 && m.Ltp > m.Prev5mHigh + buffer));
        }

        // 5: RSI
        if (Active("CallRsiFloor")&& Active("CallRsiCeiling"))
        {
            var (floor, ceiling) = OpeningRsiBand(
                symbol, call: true, Threshold("CallRsiFloor", 60m), Threshold("CallRsiCeiling", 70m), m);

            // Entry RSI is the 5m RSI(14). Null (fewer than 15 closed 5m bars) fails closed.
            var rsi5m = m.Rsi5m;
            var passed = rsi5m.HasValue && rsi5m.Value >= floor && rsi5m.Value <= ceiling;
            var description = $"5 5m RSI in [{floor},{ceiling}]";

            if (Active("RsiCallPullbackFloor")&& Active("RsiCallPullbackCeiling"))
            {
                var pullbackFloor = Threshold("RsiCallPullbackFloor", 30m);
                var pullbackCeiling = Threshold("RsiCallPullbackCeiling", 40m);

                passed |= rsi5m.HasValue && rsi5m.Value >= pullbackFloor && rsi5m.Value <= pullbackCeiling;
                description += $" or [{pullbackFloor},{pullbackCeiling}]";
            }

            conditions.Add((
                $"{description} - 5m RSI was {(rsi5m.HasValue ? rsi5m.Value.ToString("F2") : "N/A")} (1m RSI {m.Rsi:F2}, not used for entry)",
                passed));
        }

        // 6 + 7: Bullish candle
        if (Active("CallBodyPctMinimum"))
        {
            var minimum = Threshold("CallBodyPctMinimum", 30m);
            var (candleBullish, _) = ConfirmEntryCandle(m.LastClosedCandle, minimum);

            conditions.Add((
                $"6+7 Bullish candle, body >= {minimum}%",
                candleBullish));
        }

        // 8 - Volume > VolumeSMA20. Not wired for the same reason as condition 3: Groww publishes
        // no index candle volume, so every Candle.Volume is 0 and CurrentVolume/AvgVolume20 are
        // always 0 (verified 2026-09-02: MAX(Volume)=0 on every series, every symbol) - there is
        // no data for this condition to read.

        // 9: ATR
        if (Active("CallMinAtrPercent"))
        {
            var minimum = Threshold("CallMinAtrPercent", 0.03m);

            conditions.Add((
                $"9 ATR% >= {minimum:F4} - was {m.AtrPct:F4}",
                m.AtrPct >= minimum));
        }

        // 10: Delta - controlled only by threshold activation, no separate enable flag
        if (Active("CallDeltaMinimum") && Active("CallDeltaMaximum"))
        {
            var minimum = Threshold("CallDeltaMinimum", 0.35m);
            var maximum = Threshold("CallDeltaMaximum", 0.65m);

            conditions.Add((
                $"10 CE Delta in [{minimum},{maximum}] - was {m.CeAtmDelta:F3}",
                m.CeAtmDelta >= minimum && m.CeAtmDelta <= maximum));
        }

        // 11: Day change or recovery
        if (Active("CallDayChangePctMin")
            && Active("CallRecoveryPctMin"))
        {
            var dayMinimum = Threshold("CallDayChangePctMin", 0.10m);
            var recoveryMinimum = Threshold("CallRecoveryPctMin", 0.40m);
            var recoveryPct = m.DayLow > 0
                ? (m.Ltp - m.DayLow) / m.DayLow * 100m
                : 0m;

            conditions.Add((
                $"11 DayChange% > {dayMinimum} (was {m.DayChangePct:F3}) " +
                $"OR Recovery% > {recoveryMinimum} (was {recoveryPct:F3}) with LTP > DayOpen",
                m.DayChangePct > dayMinimum
                    || (recoveryPct > recoveryMinimum
                        && m.DayOpen > 0
                        && m.Ltp > m.DayOpen)));
        }

        // 12: PCR - off by default (EnableCallPcrFilter). Historically counter-indicated on the
        // closed trades analysed (CALL winners averaged PCR 0.90, losers 1.17), so this must be
        // turned on deliberately per symbol after a fresh backtest.
        // Not checked on a gap-up session (same gap definition as OpeningRsiBand); a missing gap counts as not gap-up.
        if (Enabled("EnableCallPcrFilter", false) && Active("CallPcrMinimum") && !IsGapUpSession(symbol, m))
        {
            var minimum = Threshold("CallPcrMinimum", 1.05m);

            conditions.Add((
                $"12 PCR >= {minimum} - was {m.Pcr:F3}",
                m.Pcr >= minimum));
        }

        // 12b: PCR ceiling - CALL only, applies once a CallPcrMaximum row exists and is active (no Enable flag; an
        // unconfigured key is off). 1.5 is the compiled default if a row carries no usable value.
        if (Active("CallPcrMaximum"))
        {
            var maximum = Threshold("CallPcrMaximum", 1.5m);

            conditions.Add((
                $"12b PCR <= {maximum} - was {m.Pcr:F3}",
                m.Pcr <= maximum));
        }

        // 13: Open-interest change
        if (Active("CallOiChangeMinimum"))
        {
            var minimum = Threshold("CallOiChangeMinimum", -1000m);

            conditions.Add((
                $"13 CE ChangeInOi >= {minimum} - was {m.CeChangeInOi}",
                m.CeChangeInOi >= minimum));
        }


        if (Active("PriceChangeThreshold"))
        {
            var threshold = Threshold("PriceChangeThreshold", 0.10m);

            conditions.Add((
                $"[retired] PriceChangePct > {threshold}",
                m.PriceChangePct > threshold));
        }

        return new DirectionVerdict(conditions.ToArray());
    }

    /// <summary>
    /// PUT entry specification - the exact mirror of <see cref="EvaluateCall"/>, including the
    /// list-based construction: a condition is appended only when its Enable* flag (if any) is on
    /// AND every threshold it reads is IsSettingActive, so DirectionVerdict.Checks only ever shows
    /// conditions that were actually evaluated.
    ///
    ///   1 EMA20 &lt; EMA50             7 Body% >= PutBodyPctMinimum
    ///   2 LTP &lt; EMA20               8 Volume &gt; VolumeSMA20  (NOT WIRED - no volume data)
    ///   3 LTP &lt; VWAP  (NOT WIRED - no index volume, so no VWAP)
    ///   4 LTP &lt; Prev 5m Low         9 ATR% >= PutMinAtrPercent
    ///   5 RSI PutRsiFloor..Ceiling   10 |PE Delta| in [PutDeltaMinimum(0.35), PutDeltaMaximum(0.65)]
    ///   6 Bearish candle            11 DayChange% &lt; max OR (Fall% > min AND LTP &lt; DayOpen)
    ///                               12 PCR &lt;= PutPcrMaximum (off by default - see condition 12)
    ///                              12b PCR &gt;= PutPcrMinimum (off by default - see condition 12b)
    ///
    /// Fall% = (DayHigh - LTP) / DayHigh * 100.
    ///
    /// DELTA SIGN is the one place the mirror is not literal: a put's delta is NEGATIVE (-0.50 for
    /// an ATM PE), so the band is applied to the ABSOLUTE value. Comparing the raw negative
    /// against a positive band would reject every put ever quoted.
    /// </summary>
    internal DirectionVerdict EvaluatePut(string symbol, in EntryMarketInputs m)
    {
        var conditions = new List<(string Description, bool Passed)>();

        bool Active(string key) =>
            _strategySettings.IsSettingActive(key, symbol);

        bool Enabled(string key, bool defaultValue = true) =>
            _strategySettings.GetBool(key, symbol, defaultValue);

        decimal Threshold(string key, decimal defaultValue) =>
            _strategySettings.GetDecimal(key, symbol, defaultValue);

        // 1 + 2: Trend
        if (Enabled("EnableTrendFilter"))
        {
            conditions.Add((
                $"1 EMA20 < EMA50 ({m.Ema20:F2} vs {m.Ema50:F2})",
                m.Ema20 < m.Ema50));

            conditions.Add((
                $"2 LTP < EMA20 ({m.Ltp:F2} vs {m.Ema20:F2})",
                m.Ltp < m.Ema20));
        }

        // 2b: EMA gap (shared CALL/PUT condition, off by default - see EmaGapCondition).
        if (EmaGapCondition.Evaluate(m.Ema20, m.Ema50, Enabled, Active, Threshold) is { } emaGap)
            conditions.Add(emaGap);


        // 3 - LTP < VWAP. Not wired for the same reason as the CALL side: no index volume means
        // no VWAP to compare against.

        // 4: Previous five-minute low
        if (Enabled("EnablePutPrev5mLowFilter"))
        {
            conditions.Add((
                $"4 LTP < Prev5mLow ({(m.Prev5mLow > 0 ? m.Prev5mLow.ToString("F2") : "unavailable")})",
                m.Prev5mLow > 0 && m.Ltp < m.Prev5mLow));
        }

        // 5: RSI
        if (Active("PutRsiFloor")
            && Active("PutRsiCeiling"))
        {
            var (floor, ceiling) = OpeningRsiBand(
                symbol, call: false, Threshold("PutRsiFloor", 28m), Threshold("PutRsiCeiling", 40m), m);

            // Entry RSI is the 5m RSI(14). Null (fewer than 15 closed 5m bars) fails closed.
            var rsi5m = m.Rsi5m;
            var passed = rsi5m.HasValue && rsi5m.Value >= floor && rsi5m.Value <= ceiling;
            var description = $"5 5m RSI in [{floor},{ceiling}]";
            // Optional second PUT band (EnablePutRsiBand2, off by default, per-symbol capable).
            // Widens the accepted RSI to the union of [floor,ceiling] and [Band2Floor,Band2Ceiling]
            // without touching the 35-39 gap between them.
            if (Enabled("EnablePutRsiBand2", false)
                && Active("PutRsiBand2Floor")
                && Active("PutRsiBand2Ceiling"))
            {
                var band2Floor = Threshold("PutRsiBand2Floor", 40m);
                var band2Ceiling = Threshold("PutRsiBand2Ceiling", 49m);

                passed |= rsi5m.HasValue && rsi5m.Value >= band2Floor && rsi5m.Value <= band2Ceiling;
                description += $" or [{band2Floor},{band2Ceiling}]";
            }

            conditions.Add((
                $"{description} - 5m RSI was {(rsi5m.HasValue ? rsi5m.Value.ToString("F2") : "N/A")} (1m RSI {m.Rsi:F2}, not used for entry)",
                passed));
        }

        // 6 + 7: Bearish candle
        if (Enabled("EnableCandleFilter") && Active("PutBodyPctMinimum"))
        {
            var minimum = Threshold("PutBodyPctMinimum", 30m);
            var (_, candleBearish) = ConfirmEntryCandle(m.LastClosedCandle, minimum);

            conditions.Add((
                $"6+7 Bearish candle, body >= {minimum}%",
                candleBearish));
        }

        // 8 - Volume > VolumeSMA20. Not wired for the same reason as the CALL side: no index
        // candle volume exists, so the condition has nothing to read.

        // 9: ATR
        if (Enabled("EnablePutAtrFilter") && Active("PutMinAtrPercent"))
        {
            var minimum = Threshold("PutMinAtrPercent", 0.03m);

            conditions.Add((
                $"9 ATR% >= {minimum:F4} - was {m.AtrPct:F4}",
                m.AtrPct >= minimum));
        }

        // 10: |PE Delta| - controlled only by threshold activation, no separate enable flag.
        // DELTA SIGN: a put's delta is NEGATIVE (-0.50 for an ATM PE), so the band is applied to
        // the absolute value - comparing the raw negative against a positive band would reject
        // every put ever quoted.
        if (Active("PutDeltaMinimum") && Active("PutDeltaMaximum"))
        {
            var minimum = Threshold("PutDeltaMinimum", 0.35m);
            var maximum = Threshold("PutDeltaMaximum", 0.65m);

            conditions.Add((
                $"10 |PE Delta| in [{minimum},{maximum}] - was {m.PeAtmDelta:F3}",
                Math.Abs(m.PeAtmDelta) >= minimum && Math.Abs(m.PeAtmDelta) <= maximum));
        }

        // 11: Day change or fall. Fall% = (DayHigh - LTP) / DayHigh * 100.
        if (Enabled("EnablePutDayFilter")
            && Active("PutDayChangePctMax")
            && Active("PutFallPctMin"))
        {
            var dayMaximum = Threshold("PutDayChangePctMax", -0.10m);
            var fallMinimum = Threshold("PutFallPctMin", 0.40m);
            var fallPct = m.DayHigh > 0
                ? (m.DayHigh - m.Ltp) / m.DayHigh * 100m
                : 0m;

            conditions.Add((
                $"11 DayChange% < {dayMaximum} (was {m.DayChangePct:F3}) " +
                $"OR Fall% > {fallMinimum} (was {fallPct:F3}) with LTP < DayOpen",
                m.DayChangePct < dayMaximum
                    || (fallPct > fallMinimum
                        && m.DayOpen > 0
                        && m.Ltp < m.DayOpen)));
        }

        // 12: PCR - off by default (EnablePutPcrFilter). On the closed trades analysed, PUT
        // winners/losers overlapped almost completely on PCR, so this discriminated nothing;
        // enable per symbol only after a fresh backtest supports it.
        if (Enabled("EnablePutPcrFilter", false) && Active("PutPcrMaximum"))
        {
            var maximum = Threshold("PutPcrMaximum", 0.90m);

            conditions.Add((
                $"12 PCR <= {maximum} - was {m.Pcr:F3}",
                m.Pcr <= maximum));
        }

        // 12b: PCR floor - applies only where PutPcrMinimum has an active row. Meant per symbol:
        // on 2026-09-30 SENSEX PUT trend minutes with PCR < 0.70 lost (212 trades, 7 days, PF 0.50)
        // while BANKNIFTY and NIFTY did not (docs/research/2026-09-30-backtest-condition-put).
        if (Enabled("EnablePutPcrFilter", false) && Active("PutPcrMinimum"))
        {
            var minimum = Threshold("PutPcrMinimum", 0.70m);

            conditions.Add((
                $"12b PCR >= {minimum} - was {m.Pcr:F3}",
                m.Pcr >= minimum));
        }

        // 13: Open-interest change. See the CALL side for why this defaults to the -1000
        // tolerance rather than a strict "> 0".
        if (Active("PutOiChangeMinimum"))
        {
            var minimum = Threshold("PutOiChangeMinimum", -1000m);

            conditions.Add((
                $"13 PE ChangeInOi >= {minimum} - was {m.PeChangeInOi}",
                m.PeChangeInOi >= minimum));
        }

        if (Active("PriceChangeThreshold"))
        {
            var threshold = Threshold("PriceChangeThreshold", 0.10m);

            conditions.Add((
                $"[retired] PriceChangePct < -{threshold}",
                m.PriceChangePct < -threshold));
        }

        return new DirectionVerdict(conditions.ToArray());
    }

    /// <summary>
    /// THE entry-candle confirmation mechanism, shared by the AND-chain and by the approved-rule
    /// path so both ask the identical question of the identical candle.
    ///
    /// The candle passed in is always a CLOSED bar — MarketSnapshotBuilder excludes the forming
    /// one from both series before LastClosed1mCandle / LastClosed5mCandle are set.
    ///
    /// Confirmation is direction PLUS body strength: a doji or a long-wicked bar that closed the
    /// right way is not a confirmation. bodyPctMinimum is the existing per-symbol setting.
    /// </summary>
    public static (bool Bullish, bool Bearish) ConfirmEntryCandle(Candle? candle, decimal bodyPctMinimum)
    {
        if (candle == null) return (false, false);

        var range = candle.High - candle.Low;
        if (range <= 0) return (false, false);

        var bodyPct = Math.Abs(candle.Close - candle.Open) / range * 100m;
        if (bodyPct < bodyPctMinimum) return (false, false);

        return (candle.Close > candle.Open, candle.Close < candle.Open);
    }

    /// <summary>
    /// Builds a signal for a pre-approved single-signal RULE match (see SingleSignalRuleEvaluator),
    /// bypassing GenerateSignalDetailed's combined AND-chain — the rule's own conditions have
    /// already been checked and were validated as a set by backtest, so re-imposing the AND-chain
    /// would reject setups the win rate was measured on.
    ///
    /// What is NOT bypassed: option-leg selection, index/option SL/T1/T2 and the SignalBuilder all
    /// come from the same <see cref="BuildSignalResult"/> the normal path uses, unmodified.
    ///
    /// <paramref name="confidence"/> is computed here purely so the real value reaches the audit
    /// row and the log line. It is NOT a gate for this path.
    /// </summary>
    public SignalGenerationResult GenerateRuleSignal(
        string symbol,
        string optionType,
        bool call,
        decimal ltp,
        decimal ema20,
        decimal ema50,
        decimal pcr,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume20,
        decimal atrRaw,
        List<OptionChainData>? optionChain,
        Candle? lastClosedCandle,
        out decimal confidence)
    {
        confidence = 0m;

        if (optionChain == null || optionChain.Count == 0)
            return SignalGenerationResult.Failed("No option chain data available");

        // Body% of the trigger candle — an input to ComputeConfidence only. A missing or
        // zero-range candle leaves it at 0 rather than failing the rule: candle quality is not
        // one of the rule's conditions, so it must not be able to suppress the signal.
        var bodyPct = 0m;
        if (lastClosedCandle != null)
        {
            var range = lastClosedCandle.High - lastClosedCandle.Low;
            if (range > 0)
                bodyPct = Math.Abs(lastClosedCandle.Close - lastClosedCandle.Open) / range * 100m;
        }

        var bodyPctMinimum = _strategySettings.GetDecimal("BodyPctMinimum", symbol, 35m);
        var (rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling) = RsiConfidenceBand(symbol);

        var rsiConfidenceActive = IsRsiConfidenceActive(symbol, call);

        confidence = ComputeConfidence(
            call, ema20, ema50, ltp, pcr, rsi, currentVolume, avgVolume20, bodyPct,
            rsiCallFloor, rsiCallCeiling, rsiPutFloor, rsiPutCeiling, bodyPctMinimum, rsiConfidenceActive);

        // Mirrors GenerateSignalDetailed's ATM resolution exactly. Duplicated rather than
        // extracted because strike selection is explicitly out of scope for this change; unify
        // the two once that is approved separately.
        var interval = (decimal)_instrumentCache.GetStrikeInterval(symbol);
        var spot = optionChain
            .Where(x => x.UnderlyingLtp > 0)
            .Select(x => x.UnderlyingLtp)
            .FirstOrDefault();

        if (spot <= 0)
            spot = ltp;

        var atmStrike = Math.Round(spot / interval, MidpointRounding.AwayFromZero) * interval;

        return BuildSignalResult(symbol, ltp, atrRaw, optionChain, atmStrike, optionType, confidence, call);
    }

    // ── Option-leg lookup, index/option SL/target math, SignalBuilder ───────────────────────────
    private SignalGenerationResult BuildSignalResult(
        string symbol, decimal ltp, decimal atrRaw, List<OptionChainData> chain, decimal atmStrike,
        string optionType, decimal confidence, bool call)
    {
        var chainLeg = chain
    .Where(o =>
        o.OptionType == optionType &&
        o.Ltp > 0)
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


        // ── Build Signal using SignalBuilder ──────────────────────────────────
        // Buy()/Sell() MUST be called before WithIndexStops() — stops depend on direction.
        var isExpiryDay = _instrumentCache.IsExpiryDay(symbol);
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

        // stopDistance comes back from AtrRiskService ALREADY CLAMPED to
        // [MinimumIndexStopPoints, MaximumIndexStopPoints]. Targets must be derived from that
        // clamped distance, not from raw ATR, or the two legs scale differently and R:R silently
        // drifts away from the configured AtrTarget/AtrStopLoss ratio whenever the clamp binds.
        //
        // Previously the targets used `atrRaw * AtrTargetNMultiplier * delta` while risk used the
        // clamped stop. Below AtrRaw 13.33 (= MinimumIndexStopPoints / AtrStopLossMultiplier) the
        // floor widened the stop but left the targets untouched, pushing R:R under 2.0 and making
        // RiskRewardGate unsatisfiable. That covered 99.1% of observed NIFTY snapshots.
        var stopDistance = Math.Abs(ltp - sl);

        var stopMultiplier = Settings.AtrStopLossMultiplier > 0
            ? Settings.AtrStopLossMultiplier
            : 1.5m;

        var optionRisk = stopDistance * delta;
        var optionTarget1 = stopDistance * (Settings.AtrTarget1Multiplier / stopMultiplier) * delta;
        var optionTarget2 = stopDistance * (Settings.AtrTarget2Multiplier / stopMultiplier) * delta;

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

            // The TRADED leg's strike, not atmStrike: the liquid leg can be a neighbouring strike
            // (round strikes carry the volume), and storing atmStrike made StrikePrice disagree
            // with OptionSymbol/OptionLtp on 15 of 86 signals (e.g. 124: 54400 vs 54500PE).
            .ForOption(
                chainLeg.StrikePrice,
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

            // Quantity is not decided here. AdaptiveLotSizingService is the single authority and
            // overwrites Signal.Quantity in StrategyManager immediately after this returns, using
            // a fixed 1 lot. The old confidence-scaled LotsForConfidence (1/2/3 lots) and its
            // high-vega decrement were both removed: the value never survived to the broker, and
            // with the volume bonus gone the confidence ceiling is 85, making its >90 tier
            // unreachable and its >80 tier misleading.
            .WithLots(1)

            .WithConfidence(confidence);


        return SignalGenerationResult.Success(builder.Build());
    }

    private static string FormatChecklist(IEnumerable<(string Label, bool Passed)> items) =>
        string.Join(Environment.NewLine, items.Select(i => $"{(i.Passed ? "✓" : "✗")} {i.Label}"));
}
