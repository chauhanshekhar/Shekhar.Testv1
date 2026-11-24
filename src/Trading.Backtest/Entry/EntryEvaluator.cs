using Microsoft.Extensions.Logging.Abstractions;
using Trading.Backtest.Data;
using Trading.Backtest.Settings;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.SignalEngine.Strategies;
using Trading.Worker.SignalGates;

namespace Trading.Backtest.Entry;

public enum TradeSide { Call, Put }

public enum SelectorMode
{
    /// <summary>Assume StrategySelector chooses Directional whenever the CALL/PUT chain passes (the only choice for price-only history).</summary>
    Ignore,
    /// <summary>Require the stored snapshot's StrategySelected = Directional; points without a snapshot are blocked.</summary>
    UseStoredSnapshot,
    /// <summary>
    /// Replay the selector from the rebuilt inputs: MarketRegimeGate (Low or Extreme volatility blocks), the MarketState routing
    /// (Range, Reversal, NoTrade and News run no engine) and the Sideways-regime suppression of Directional. HeroZero win-rate
    /// contests and expiry-day windows are not replayed (Directional is assumed).
    /// </summary>
    Replay,
}

public enum SidewaysOverride
{
    /// <summary>Live behaviour: a Sideways regime suppresses Directional.</summary>
    None,
    /// <summary>Do not suppress when the 5m trend agrees with the side (5m EMA20 above EMA50 for CALL, below for PUT).</summary>
    When5mAgrees,
    /// <summary>Never suppress on the Sideways regime.</summary>
    Always,
}

/// <summary>
/// Entry-rule experiments on top of the live rules; every default reproduces production. They apply only in <see cref="SelectorMode.Replay"/>
/// where noted. The gap-trend continuation rule is active for a side when the open gapped at least <see cref="GapMinPct"/> in that
/// direction and the 5m trend already agrees (a gap that keeps going).
/// </summary>
public sealed record EntryRules
{
    public SidewaysOverride Sideways { get; init; } = SidewaysOverride.None;
    /// <summary>Research: MarketStates (Range, Reversal) in which the selector lets Directional run anyway. Null = live routing.
    /// NoTrade and News are never overridable (they carry the event/no-trade safety).</summary>
    public IReadOnlyList<string>? SelectorAllowStates { get; init; }
    /// <summary>Research: name the blocking MarketState in the gate label ("StrategySelector:Reversal") so the signal log shows it.</summary>
    public bool LabelSelectorState { get; init; }
    public bool GapTrendContinuation { get; init; }
    public double GapMinPct { get; init; } = 0.30;
    /// <summary>While the gap trend is active, the CALL 5m RSI ceiling (null = unchanged).</summary>
    public decimal? GapTrendCallRsiCeiling { get; init; }
    /// <summary>While the gap trend is active, the PUT 5m RSI floor (null = unchanged).</summary>
    public decimal? GapTrendPutRsiFloor { get; init; }
    public bool GapTrendSkipsSidewaysSuppression { get; init; }
    public bool GapTrendSkipsEarlyTrendGate { get; init; }

    /// <summary>Research only: skip the replayed BreakoutProtectionService even when StrategySettings.EnableBreakoutProtection is on.</summary>
    public bool SkipBreakoutProtection { get; init; }
    public bool GapTrendWaivesEmaGapFilter { get; init; }

    // ── the proposed gap-up continuation entry (CALL, only while the gap trend is active) ──────────────────────────
    /// <summary>Pullback toward EMA20 plus a bullish last bar: the lowest low of the last 10 bars within PullbackAtr x ATR of EMA20, last bar closes up and above EMA20.</summary>
    public bool GapUpPullback { get; init; }
    public double PullbackAtr { get; init; } = 0.25;
    /// <summary>Confirmed breakout: the last closed bar is bullish and closes above the highest high of the 10 bars before it.</summary>
    public bool GapUpBreakout { get; init; }
    /// <summary>Exhaustion filter: no new CALL when LTP is more than this many ATRs (index points) above EMA20.</summary>
    public double? GapUpExhaustionAtr { get; init; }
    /// <summary>While the CALL gap-up mode is active (gap up, 5m trend up): block PUT signals from the chain, or from the chain and the BANKNIFTY breakdown rule.</summary>
    public CounterTrendPut GapUpBlocksPut { get; init; } = CounterTrendPut.Allow;

    /// <summary>
    /// Block an entry when price has already run more than this many ATRs beyond the 1m EMA20 in the
    /// trade's own direction - the "late entry" test. Null = no cap (production). Measured as
    /// (Ltp - Ema20) / Atr for CALL and (Ema20 - Ltp) / Atr for PUT, so a negative value means price
    /// has not yet reached EMA20 on the trade's side.
    /// </summary>
    public double? MaxStretchAtr { get; init; }
    /// <summary>The mirror: require AT LEAST this much stretch. Used only as a contrast arm.</summary>
    public double? MinStretchAtr { get; init; }

    /// <summary>Research: require the 1m EMA9 above EMA21 for a CALL (below for a PUT). False = production.</summary>
    public bool RequireFastEmaAligned { get; init; }

    /// <summary>Research: EarlyTrendGate exception. A CALL the gate would block (5m EMA20 above EMA50) is allowed while (EMA20-EMA50)/ATR5m is below this. Null = gate unchanged.</summary>
    public double? EarlyTrendMaxGapAtr { get; init; }

    /// <summary>Research: the EarlyTrendMaxGapAtr exception only applies at or after this IST minute of day (630 = 10:30). Null = no time limit.</summary>
    public int? EarlyTrendMaxGapAtrFromMinute { get; init; }

    /// <summary>Research: the EarlyTrendMaxGapAtr exception only applies on sessions that opened GapUp or GapDown (OpeningTypeThresholdPct). Normal-day sessions keep the live gate.</summary>
    public bool EarlyTrendMaxGapAtrGapDaysOnly { get; init; }

    /// <summary>Research (Version D): a CALL on a gap-up session (open >= GapMinPct above the previous close) is exempt from the EarlyTrendGate before this IST minute of day (600 = 10:00). Null = gate unchanged.</summary>
    public int? EarlyTrendGapUpMorningUntilMinute { get; init; }

    /// <summary>
    /// Apply <see cref="MaxStretchAtr"/> ONLY when the session so far looks range-bound, measured by
    /// how directional its travel has been:
    ///
    ///     efficiency = |Ltp - SessionOpen| / (DayHigh - DayLow)
    ///
    /// 0 means price has round-tripped back to the open; 1 means it has gone one way all session.
    /// This is the entry-time analogue of the DecisiveFraction test SessionClassifier applies at the
    /// close - but unlike the classifier it uses ONLY running values (DayHigh/DayLow accumulate over
    /// bars strictly before the current one), so it is safe in a gate. Null = cap always applies.
    /// </summary>
    public double? CapOnlyWhenEfficiencyBelow { get; init; }

    /// <summary>
    /// Early in a session the high-low range is tiny and the ratio above is unstable, so efficiency
    /// is treated as unknown until the range reaches this many ATRs.
    /// </summary>
    public double EfficiencyMinRangeAtr { get; init; } = 0.75;

    /// <summary>What to do while efficiency is unknown: true = apply the cap, false = let entries through.</summary>
    public bool CapWhenEfficiencyUnknown { get; init; } = true;

    /// <summary>Restrict entries to sessions that opened this way (Any = no restriction, i.e. production).</summary>
    public OpeningType OpeningTypeFilter { get; init; } = OpeningType.Any;
    /// <summary>Gap size in percent separating GapUp / Normal / GapDown.</summary>
    public double OpeningTypeThresholdPct { get; init; } = 0.30;

    // ── opening-conditional RSI band ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Replace the side's RSI band on sessions that opened with a gap of at least
    /// <see cref="OpeningBandGapPct"/> in the named direction. The gap is known at the open, so this
    /// is a legitimate entry rule - unlike the continuation/reversal label, which is only knowable
    /// at the close. Null = the normal band applies on every session.
    /// </summary>
    public OpeningType OpeningBandAppliesTo { get; init; } = OpeningType.Any;
    public decimal? OpeningBandRsiFloor { get; init; }
    public decimal? OpeningBandRsiCeiling { get; init; }
    public double OpeningBandGapPct { get; init; } = 0.30;
    /// <summary>Refuse the side entirely on those sessions (tested as a contrast to re-banding).</summary>
    public bool OpeningBandBlocks { get; init; }

    /// <summary>
    /// Restrict entries to an IST time window, in minutes after midnight (e.g. 11*60 to 14*60).
    /// Null = the live window. Time of day is known at the entry minute, so this is a legitimate rule.
    /// </summary>
    public int? EntryFromMinuteIst { get; init; }
    public int? EntryToMinuteIst { get; init; }

    // ── session trend ("going up and up" / "down and down") ───────────────────────────────────────
    /// <summary>
    /// Require the session SO FAR to be travelling in the trade's direction before entering:
    ///
    ///     trendSoFar = (Ltp - SessionOpen) / Atr        signed, in ATRs
    ///     efficiency = |Ltp - SessionOpen| / (DayHigh - DayLow)   0 = round-tripped, 1 = one-way
    ///     nearExtreme = distance from the session low (for a PUT) or high (for a CALL), in ATRs
    ///
    /// All three use running values - DayHigh/DayLow accumulate over bars strictly BEFORE the current
    /// one - so this is a legitimate gate, unlike the end-of-day Continuation/Reversal label.
    ///
    /// Direction is relative to the trade: a CALL wants trendSoFar >= +MinMove, a PUT <= -MinMove.
    /// Set <see cref="SessionTrendAppliesTo"/> to limit it to one kind of opening; Any = every session.
    /// Null MinMove = the rule is off.
    /// </summary>
    public double? SessionTrendMinMoveAtr { get; init; }
    public double SessionTrendMinEfficiency { get; init; } = 0.50;
    /// <summary>Optional: also require price to be within this many ATRs of the session extreme.</summary>
    public double? SessionTrendNearExtremeAtr { get; init; }
    /// <summary>Which openings the rule applies to. Any = all of them.</summary>
    public OpeningType SessionTrendAppliesTo { get; init; } = OpeningType.Any;
    /// <summary>Gap size separating GapUp / Normal / GapDown for the line above.</summary>
    public double SessionTrendGapPct { get; init; } = 0.30;

    /// <summary>A dedicated CALL strategy that replaces the chain (null = production entry).</summary>
    public TrendStrategyRules? Trend { get; init; }

    /// <summary>Rated direction (no RSI): CALL vs PUT score, the winning side replaces the chain (null = production entry).</summary>
    public RatedDirectionRules? Rated { get; init; }

    /// <summary>Research: a candidate entry condition read from IndicatorSnapshots features (null = production entry).</summary>
    public ProposedEntryRule? Proposed { get; init; }
}

/// <summary>EMA alignment as the snapshot analysis defines it (1m price/EMA20/EMA50 plus the 5m EMA20 vs EMA50).</summary>
public enum EmaAlignment { Any, Bullish, Bearish, Mixed }

/// <summary>
/// A candidate entry condition expressed on the same features the IndicatorSnapshots analysis used: opening type, 1-minute RSI band,
/// ATR% window and EMA alignment. <see cref="ReplacesChain"/> = true skips the live AND-chain (the candidate is the entry rule) and keeps only
/// the safety gates; false requires the live chain to pass as well, so the candidate can only tighten production.
/// All bounds are half-open: Rsi1m in [Floor, Ceiling), AtrPct in [Min, Max).
/// </summary>
public sealed record ProposedEntryRule
{
    public OpeningType Opening { get; init; } = OpeningType.Any;
    public double OpeningThresholdPct { get; init; } = 0.30;
    public double Rsi1mFloor { get; init; }
    public double Rsi1mCeiling { get; init; } = 100;
    public double AtrPctMin { get; init; }
    public double AtrPctMax { get; init; } = double.MaxValue;
    public EmaAlignment Alignment { get; init; } = EmaAlignment.Any;
    public bool ReplacesChain { get; init; }
}

public enum CounterTrendPut { Allow, BlockChain, BlockChainAndRule }

/// <summary>How the session opened, from the gap between the first bar's open and the previous session's close.</summary>
public enum OpeningType
{
    /// <summary>No restriction - reproduces production.</summary>
    Any,
    /// <summary>Opening gap at or above +threshold.</summary>
    GapUp,
    /// <summary>Opening gap at or below -threshold.</summary>
    GapDown,
    /// <summary>Opening gap strictly between -threshold and +threshold.</summary>
    Normal,
}

/// <summary>Dedicated CALL entry paths that replace the RSI-band chain (no RSI band, PCR, EMA-gap rule, Sideways suppression or early-trend gate).</summary>
public enum TrendStrategy
{
    None,
    /// <summary>Momentum breakout: price above the session open, EMA20 and a rising 5m trend, RSI HIGH (>= MinRsi5), enter on a bullish close above the 10-bar high.</summary>
    Momentum,
    /// <summary>Gap that holds: gap up, never gave back half the gap, trend aligned, enter on a pullback toward EMA20 with a bullish bar.</summary>
    GapHold,
    /// <summary>Enter early in the move: within MaxBarsSinceCross minutes of the 1m EMA20 rising above EMA50, price above EMA20, bullish bar, 5m RSI in band.</summary>
    EarlyCross,
    /// <summary>Gap-up-only momentum setup: gap, 5m and 1m EMA20 above EMA50, price above EMA20, 5m RSI band, ATR% floor, 1m close above the previous completed 5m high.</summary>
    GapUpMomentum,
}

/// <summary>Parameters of the dedicated trend strategies. IST minutes after midnight for the entry window.</summary>
public sealed record TrendStrategyRules
{
    public TrendStrategy Strategy { get; init; } = TrendStrategy.None;
    public bool RequireGap { get; init; } = true;
    public double GapMinPct { get; init; } = 0.30;
    public double MinUpFromOpenPct { get; init; } = 0.0;
    public decimal MinRsi5 { get; init; } = 60m;
    public int StartMinute { get; init; } = 9 * 60 + 20;
    public int EndMinute { get; init; } = 14 * 60;
    public double PullbackAtr { get; init; } = 0.5;
    /// <summary>Fraction of the opening gap the market may give back before the gap counts as failed (GapHold).</summary>
    public double MaxGapFill { get; init; } = 0.5;
    public int MaxTradesPerSymbolDay { get; init; } = 2;

    // ── EarlyCross only ───────────────────────────────────────────────────────────────────────────────────────────
    public int MaxBarsSinceCross { get; init; } = 10;
    public decimal MaxRsi5 { get; init; } = 70m;
    public bool RequireBullishBar { get; init; } = true;
    /// <summary>Only while the 5m trend has not turned up yet (5m EMA20 not above EMA50), the early-trend gate's principle.</summary>
    public bool Require5mNotUp { get; init; }
    /// <summary>Apply the live EMA-gap rule (EmaGapMinPoints for the symbol).</summary>
    public bool RequireEmaGap { get; init; }
    /// <summary>Run the replayed strategy selector (Range/Reversal/NoTrade/News/Sideways block) before the entry.</summary>
    public bool ApplySelector { get; init; }

    // ── GapUpMomentum only ───────────────────────────────────────────────────────────────────────────────────────
    public decimal MinRsi5Floor { get; init; } = 45m;
    public double MinAtrPct { get; init; } = 0.03;
    /// <summary>Require the last closed 1m bar to close above the previous completed 5m high.</summary>
    public bool RequireAbovePrev5mHigh { get; init; } = true;
}

/// <summary>Thresholds for <see cref="RatedDirectionScorer"/>; swept by --rated instead of read from settings rows.</summary>
public sealed record RatedDirectionRules(decimal MinScore, decimal MinMargin);

/// <param name="SignalProduced">The CALL/PUT chain (or the approved rule) produced a signal - before any gate.</param>
/// <param name="Candidate">The signal also passed every replayable state-free gate.</param>
public sealed record EntryVerdict(bool SignalProduced, bool Candidate, string? BlockedBy, string? Reason, string Path)
{
    public static EntryVerdict Pass(string path) => new(true, true, null, null, path);
    public static EntryVerdict GateBlock(string by, string reason, string path) => new(true, false, by, reason, path);
    public static EntryVerdict NoSignal(string by, string reason) => new(false, false, by, reason, "");
}

/// <summary>
/// Static (state-free) entry decision for one decision minute: the pre-gates that can be replayed (AtrGate), the REAL
/// CALL/PUT AND-chain (SignalEngineService.EvaluateCall / EvaluatePut), the BANKNIFTY PUT breakdown rule as the live
/// alternative path (StrategyManager reaches it only when the chain produced no signal), and the post-gates that are pure
/// functions of the point (IndexStopFloorGate, EarlyTrendGate, NoTradeWindowGate).
/// Stateful gates (one position, handoff and duplicate cooldowns) live in the simulator.
///
/// NOT replayed, and reported as such: KillSwitch, EventBlackout, StaleData, MarketRegimeGate, RiskGate, RiskRewardGate,
/// SpreadGate, lot sizing, and the StaleEntryGate (ideal data has no feed lag, so its adverse-move check cannot fire).
/// </summary>
public sealed class EntryEvaluator
{
    private readonly BacktestSettings _settings;
    private readonly SignalEngineService _engine;
    private readonly SingleSignalRuleEvaluator _ruleEvaluator;
    private readonly TradingSettings _trading;
    private readonly SelectorMode _selectorMode;
    private readonly EntryRules _rules;
    private readonly SignalEngineService? _gapEngine;
    private readonly SignalEngineService? _openingBandEngine;
    private readonly RatedDirectionScorer _rated;
    private readonly TradingMode? _tradingMode;
    private readonly FiveMinuteConfirmationService _fiveMinute = new();

    /// <param name="tradingMode">
    /// The live trading mode whose trigger candle is replayed (StrategyManager.UseOneMinuteTrigger: Paper defaults to the last
    /// closed 5m bar, Live/HeroZero to the last closed 1m bar, UseOneMinuteEntryCandle overrides). Null = always the 1m bar
    /// (the replay's behaviour before live parity; kept for hand-built test points).
    /// </param>
    public EntryEvaluator(BacktestSettings settings, TradingSettings trading, IReadOnlyList<TradingInstrument> instruments,
        SelectorMode selectorMode = SelectorMode.Ignore, EntryRules? rules = null, TradingMode? tradingMode = null)
    {
        _settings = settings;
        _tradingMode = tradingMode;
        _trading = trading;
        _selectorMode = selectorMode;
        _rules = rules ?? new EntryRules();
        _engine = NewEngine(settings, trading, instruments);
        _ruleEvaluator = new SingleSignalRuleEvaluator(settings);
        _rated = new RatedDirectionScorer(settings);

        if (_rules.GapTrendContinuation)
        {
            // the chain evaluated while the gap trend is active: same settings except the experiment's overrides
            var gap = settings.Clone();
            if (_rules.GapTrendCallRsiCeiling is { } c) gap.Set("CallRsiCeiling", null, c);
            if (_rules.GapTrendPutRsiFloor is { } f) gap.Set("PutRsiFloor", null, f);
            if (_rules.GapTrendWaivesEmaGapFilter) gap.Set("EnableEmaGapFilter", null, 0m);
            _gapEngine = NewEngine(gap, trading, instruments);
        }

        if (_rules.OpeningBandAppliesTo != OpeningType.Any
            && (_rules.OpeningBandRsiFloor is not null || _rules.OpeningBandRsiCeiling is not null))
        {
            // Same chain, one band swapped. Built once so the per-minute path stays allocation-free.
            var ob = settings.Clone();
            var floorKey = "PutRsiFloor";
            var ceilingKey = "PutRsiCeiling";
            if (_rules.OpeningBandRsiFloor is { } f2) { ob.Set("CallRsiFloor", null, f2); ob.Set(floorKey, null, f2); }
            if (_rules.OpeningBandRsiCeiling is { } c2) { ob.Set("CallRsiCeiling", null, c2); ob.Set(ceilingKey, null, c2); }
            _openingBandEngine = NewEngine(ob, trading, instruments);
        }
    }

    private static SignalEngineService NewEngine(BacktestSettings settings, TradingSettings trading, IReadOnlyList<TradingInstrument> instruments) =>
        new(new BacktestInstrumentCache(instruments),
            new SystemSettingsService(new StaticOptions<TradingSettings>(trading)),
            settings,
            new AtrRiskService(),
            new OiAnalysisService(),
            null!,                                    // IRepository<OptionChainData>: unused by the evaluators
            NullLogger<SignalEngineService>.Instance);

    /// <summary>The open gapped in the side's direction by at least GapMinPct and the 5m trend already agrees.</summary>
    private bool IsGapTrend(MarketPoint p, TradeSide side)
    {
        if (!_rules.GapTrendContinuation || p.OpenGapPct is not { } gap)
            return false;
        return side == TradeSide.Call
            ? gap >= _rules.GapMinPct && p.Ema20_5m > p.Ema50_5m && p.Ema50_5m > 0
            : gap <= -_rules.GapMinPct && p.Ema20_5m < p.Ema50_5m && p.Ema20_5m > 0;
    }

    /// <summary>Gap up and the 5m trend up, whichever side is being evaluated (used to block counter-trend PUTs).</summary>
    /// <summary>
    /// The session's opening type. OpenGapPct is null for the first session in the data (no previous close), which is not
    /// classifiable - a restricted configuration skips that session rather than assuming it opened Normal.
    /// </summary>
    /// <summary>
    /// Null when the session is travelling in the trade's direction far enough and cleanly enough;
    /// otherwise the reason it is not. Uses only values known at this minute.
    /// </summary>
    private string? SessionTrendBlock(MarketPoint p, TradeSide side, double minMove)
    {
        if (p.Atr <= 0 || p.SessionOpen <= 0) return "ATR or session open unavailable";

        var sign = side == TradeSide.Call ? 1.0 : -1.0;
        var trendSoFar = sign * (p.Ltp - p.SessionOpen) / p.Atr;
        if (trendSoFar < minMove)
            return $"session has moved {trendSoFar:F2} ATR this way, needs {minMove:F2}";

        var range = p.DayHigh - p.DayLow;
        if (range <= 0) return "no session range yet";
        var efficiency = Math.Abs(p.Ltp - p.SessionOpen) / range;
        if (efficiency < _rules.SessionTrendMinEfficiency)
            return $"session efficiency {efficiency:F2} < {_rules.SessionTrendMinEfficiency:F2} (round-tripping)";

        if (_rules.SessionTrendNearExtremeAtr is { } near)
        {
            // Still at the edge of the move, rather than having stalled after it.
            var fromExtreme = side == TradeSide.Call
                ? (p.DayHigh - p.Ltp) / p.Atr
                : (p.Ltp - p.DayLow) / p.Atr;
            if (fromExtreme > near)
                return $"{fromExtreme:F2} ATR off the session extreme, needs <= {near:F2}";
        }
        return null;
    }

    /// <summary>
    /// How directional the session has been so far, in [0, 1]. Null until the session's range is
    /// wide enough for the ratio to mean anything (see <see cref="EntryRules.EfficiencyMinRangeAtr"/>).
    /// Uses only values known at this minute.
    /// </summary>
    private static double? SessionEfficiency(MarketPoint p, double minRangeAtr)
    {
        if (p.Atr <= 0 || p.SessionOpen <= 0) return null;
        var range = p.DayHigh - p.DayLow;
        if (range <= 0 || range < minRangeAtr * p.Atr) return null;
        return Math.Abs(p.Ltp - p.SessionOpen) / range;
    }

    private static OpeningType? ClassifyOpening(MarketPoint p, double thresholdPct) =>
        p.OpenGapPct is not { } gap ? null
        : gap >= thresholdPct ? OpeningType.GapUp
        : gap <= -thresholdPct ? OpeningType.GapDown
        : OpeningType.Normal;

    /// <summary>
    /// How far price has run beyond the 1m EMA20 in the trade's direction, in ATRs. Null when ATR or
    /// EMA20 is not yet warm, so a restricted configuration skips the minute rather than treating an
    /// unknown as zero.
    /// </summary>
    private static double? StretchAtr(MarketPoint p, TradeSide side)
    {
        if (p.Atr <= 0 || p.Ema20 <= 0) return null;
        var sign = side == TradeSide.Call ? 1.0 : -1.0;
        return sign * (p.Ltp - p.Ema20) / p.Atr;
    }

    private bool IsGapUpMode(MarketPoint p) =>
        _rules.GapTrendContinuation && p.OpenGapPct is { } gap && gap >= _rules.GapMinPct && p.Ema20_5m > p.Ema50_5m && p.Ema50_5m > 0;

    /// <summary>The proposed pullback / breakout / exhaustion checks for a CALL while the gap trend is active.</summary>
    private (string By, string Reason)? GapUpConfirm(MarketPoint p)
    {
        var bar = p.LastClosedBar;
        var bullish = bar.Close > bar.Open;
        if (_rules.GapUpExhaustionAtr is { } x && p.Atr > 0 && p.Ltp - p.Ema20 > x * p.Atr)
            return ("GapUpExhaustion", $"LTP {p.Ltp - p.Ema20:F1} pts above EMA20 > {x} ATR ({p.Atr:F1})");
        if (_rules.GapUpPullback && !(p.MinLow10 <= p.Ema20 + _rules.PullbackAtr * p.Atr && bullish && p.Ltp > p.Ema20))
            return ("GapUpPullback", "no pullback toward EMA20 with a bullish bar");
        if (_rules.GapUpBreakout && !(bullish && bar.Close > p.MaxHigh10Prior))
            return ("GapUpBreakout", "no bullish close above the recent 10-bar high");
        return null;
    }

    /// <summary>Entry decision of a dedicated trend strategy, followed by the safety gates every entry keeps (ATR, index-stop floor, volatility regime).</summary>
    /// <summary>
    /// The early-entry experiment: a CALL in the first minutes after the 1m EMA20 rose above EMA50. Keeps the same safety gates as the
    /// other trend strategies (volatility regime, ATR, index-stop floor); it does not use PCR or the early-trend gate.
    /// </summary>
    private EntryVerdict EvaluateEarlyCross(MarketPoint p, TrendStrategyRules t)
    {
        var minute = SessionClock.MinuteOfDayIst(p.TimeUtc);
        if (minute < t.StartMinute || minute >= t.EndMinute)
            return EntryVerdict.NoSignal("EarlyCross", "outside the entry window");
        if (p.BarsSinceEma1mUp is not { } bars || bars > t.MaxBarsSinceCross)
            return EntryVerdict.NoSignal("EarlyCross", "1m trend did not turn up within the allowed minutes");
        if (!(p.Ltp > p.Ema20 && p.Ema20 > p.Ema50))
            return EntryVerdict.NoSignal("EarlyCross", "price/EMA alignment");
        if (t.RequireBullishBar && !(p.LastClosedBar.Close > p.LastClosedBar.Open))
            return EntryVerdict.NoSignal("EarlyCross", "last 1m bar is not bullish");
        if (p.Rsi5m is not { } rsi || (decimal)rsi < t.MinRsi5 || (decimal)rsi > t.MaxRsi5)
            return EntryVerdict.NoSignal("EarlyCross", "5m RSI outside the band");
        if (t.Require5mNotUp && p.Ema20_5m > 0 && p.Ema50_5m > 0 && p.Ema20_5m > p.Ema50_5m)
            return EntryVerdict.NoSignal("EarlyCross", "5m trend already up");
        if (t.RequireEmaGap && p.Ema20 - p.Ema50 < (double)_settings.GetDecimal("EmaGapMinPoints", p.Symbol, 0m))
            return EntryVerdict.NoSignal("EarlyCross", "EMA gap below the minimum");

        var path = "TREND_EARLYCROSS";
        if (t.ApplySelector && SelectorBlock(p, TradeSide.Call, gapTrend: false) is { } sel)
            return EntryVerdict.GateBlock(sel.By, sel.Reason, path);
        if (_selectorMode == SelectorMode.Replay && p.RegimeVolatility is "Low" or "Extreme")
            return EntryVerdict.GateBlock("MarketRegimeGate", $"volatility {p.RegimeVolatility}", path);
        if (_settings.GetBool("EnableAtrFilter", p.Symbol, true) && _settings.IsSettingActive("MinAtrPercent", p.Symbol)
            && (decimal)p.AtrPct < _settings.GetDecimal("MinAtrPercent", p.Symbol, 0.10m))
            return EntryVerdict.GateBlock("AtrGate", $"ATR {p.AtrPct:F4}% below the minimum", path);
        if (LiveGates.Has<IndexStopFloorGate>() && _settings.GetBool("EnableIndexStopFloor", p.Symbol, true) && (decimal)p.Atr < IndexStopFloorGate.MinAtrRaw(_trading))
            return EntryVerdict.GateBlock("IndexStopFloorGate", $"ATR {p.Atr:F2} pts below the index stop floor", path);
        return EntryVerdict.Pass(path);
    }

    /// <summary>
    /// Rated direction: this side enters only when it wins the CALL-vs-PUT score (same scorer the live shadow uses). Keeps the
    /// live pre/post gates that are pure functions of the point (ATR, index-stop floor, early-trend, no-trade window).
    /// </summary>
    private EntryVerdict EvaluateRated(MarketPoint p, TradeSide side, string signalType, RatedDirectionRules r)
    {
        var symbol = p.Symbol;
        var bar = p.LastClosedBar;
        var input = new RatedDirectionInput((decimal)p.Ltp, (decimal)p.Ema20, (decimal)p.Ema50,
            p.Pcr is { } pcr ? (decimal)pcr : null, (decimal)p.AtrPct, (decimal)p.DayChangePct,
            bar.Close > bar.Open, bar.Close < bar.Open);
        var decision = _rated.Decide(symbol, input, r.MinScore, r.MinMargin);
        if (decision.Winner != signalType)
            return EntryVerdict.NoSignal("RatedDirection", decision.Winner is null ? decision.Reason : $"{decision.Winner} won");

        const string path = "RATED";
        if (_settings.GetBool("EnableAtrFilter", symbol, true) && _settings.IsSettingActive("MinAtrPercent", symbol)
            && (decimal)p.AtrPct < _settings.GetDecimal("MinAtrPercent", symbol, 0.10m))
            return EntryVerdict.GateBlock("AtrGate", $"ATR {p.AtrPct:F4}% below the minimum", path);
        if (LiveGates.Has<IndexStopFloorGate>() && _settings.GetBool("EnableIndexStopFloor", symbol, true) && (decimal)p.Atr < IndexStopFloorGate.MinAtrRaw(_trading))
            return EntryVerdict.GateBlock("IndexStopFloorGate", $"ATR {p.Atr:F2} pts below the index stop floor", path);
        if (LiveGates.Has<EarlyTrendGate>() && _settings.GetBool(EarlyTrendGate.EnableKey, symbol, false))
        {
            var sideSetting = _settings.GetInt(EarlyTrendGate.SideKey, symbol, EarlyTrendGate.SideCallOnly);
            if (EarlyTrendReason(p, signalType, sideSetting) is { } why)
                return EntryVerdict.GateBlock("EarlyTrendGate", why, path);
        }
        if (LiveGates.Has<NoTradeWindowGate>() && _settings.GetBool(NoTradeWindowGate.EnableKey, symbol, false))
        {
            var sideSetting = _settings.GetInt(NoTradeWindowGate.SideKey, symbol, NoTradeWindowGate.SideBoth);
            if (NoTradeWindowGate.AppliesTo(sideSetting, signalType)
                && NoTradeWindowGate.Evaluate(SessionClock.MinuteOfDayIst(p.TimeUtc),
                    _settings.GetInt(NoTradeWindowGate.StartKey, symbol, NoTradeWindowGate.DefaultStartMinute),
                    _settings.GetInt(NoTradeWindowGate.EndKey, symbol, NoTradeWindowGate.DefaultEndMinute)) is { } why)
                return EntryVerdict.GateBlock("NoTradeWindowGate", why, path);
        }
        return EntryVerdict.Pass(path);
    }

    private EntryVerdict EvaluateTrend(MarketPoint p, TrendStrategyRules t)
    {
        if (t.Strategy == TrendStrategy.EarlyCross)
            return EvaluateEarlyCross(p, t);
        if (t.Strategy == TrendStrategy.GapUpMomentum)
            return EvaluateGapUpMomentum(p, t);

        var minute = SessionClock.MinuteOfDayIst(p.TimeUtc);
        if (minute < t.StartMinute || minute >= t.EndMinute)
            return EntryVerdict.NoSignal("TrendStrategy", "outside the entry window");
        if (t.RequireGap && !(p.OpenGapPct is { } gap && gap >= t.GapMinPct))
            return EntryVerdict.NoSignal("TrendStrategy", "no qualifying gap up");
        if (p.SessionOpen <= 0 || (p.Ltp - p.SessionOpen) / p.SessionOpen * 100 < t.MinUpFromOpenPct || p.Ltp <= p.SessionOpen)
            return EntryVerdict.NoSignal("TrendStrategy", "price not above the session open");
        if (!(p.Ltp > p.Ema20 && p.Ema20 > p.Ema50))
            return EntryVerdict.NoSignal("TrendStrategy", "price/EMA alignment");
        if (p.Ema20_5m > 0 && p.Ema50_5m > 0 && p.Ema20_5m <= p.Ema50_5m)
            return EntryVerdict.NoSignal("TrendStrategy", "5m trend not up");
        if (p.Rsi5m is not { } rsi || (decimal)rsi < t.MinRsi5)
            return EntryVerdict.NoSignal("TrendStrategy", "5m RSI below the strategy minimum");

        var bar = p.LastClosedBar;
        var bullish = bar.Close > bar.Open;
        switch (t.Strategy)
        {
            case TrendStrategy.Momentum:
                if (!(bullish && bar.Close > p.MaxHigh10Prior))
                    return EntryVerdict.NoSignal("TrendStrategy", "no bullish close above the 10-bar high");
                break;
            case TrendStrategy.GapHold:
                {
                    var prevClose = p.SessionOpen / (1 + (p.OpenGapPct ?? 0) / 100.0);
                    var gapSize = p.SessionOpen - prevClose;
                    if (p.SessionLow < prevClose + (1 - t.MaxGapFill) * gapSize)
                        return EntryVerdict.NoSignal("TrendStrategy", "the gap did not hold");
                    if (!(p.MinLow10 <= p.Ema20 + t.PullbackAtr * p.Atr && bullish))
                        return EntryVerdict.NoSignal("TrendStrategy", "no pullback toward EMA20 with a bullish bar");
                    break;
                }
        }

        var path = "TREND_" + t.Strategy.ToString().ToUpperInvariant();
        if (_selectorMode == SelectorMode.Replay && p.RegimeVolatility is "Low" or "Extreme")
            return EntryVerdict.GateBlock("MarketRegimeGate", $"volatility {p.RegimeVolatility}", path);
        if (_settings.GetBool("EnableAtrFilter", p.Symbol, true) && _settings.IsSettingActive("MinAtrPercent", p.Symbol)
            && (decimal)p.AtrPct < _settings.GetDecimal("MinAtrPercent", p.Symbol, 0.10m))
            return EntryVerdict.GateBlock("AtrGate", $"ATR {p.AtrPct:F4}% below the minimum", path);
        if (LiveGates.Has<IndexStopFloorGate>() && _settings.GetBool("EnableIndexStopFloor", p.Symbol, true) && (decimal)p.Atr < IndexStopFloorGate.MinAtrRaw(_trading))
            return EntryVerdict.GateBlock("IndexStopFloorGate", $"ATR {p.Atr:F2} pts below the index stop floor", path);
        return EntryVerdict.Pass(path);
    }

    /// <summary>Research: the gap-up-only momentum setup. Uses its own ATR% floor (t.MinAtrPct) instead of the live gate; VWAP is not in the replayed series and is not applied.</summary>
    private EntryVerdict EvaluateGapUpMomentum(MarketPoint p, TrendStrategyRules t)
    {
        const string path = "TREND_GAPUPMOMENTUM";
        var minute = SessionClock.MinuteOfDayIst(p.TimeUtc);
        if (minute < t.StartMinute || minute >= t.EndMinute)
            return EntryVerdict.NoSignal("TrendStrategy", "outside the entry window");
        if (!(p.OpenGapPct is { } gap && gap >= t.GapMinPct))
            return EntryVerdict.NoSignal("TrendStrategy", "no qualifying gap up");
        if (!(p.Ema20_5m > 0 && p.Ema50_5m > 0 && p.Ema20_5m > p.Ema50_5m))
            return EntryVerdict.NoSignal("TrendStrategy", "5m trend not up");
        if (!(p.Ema20 > p.Ema50 && p.Ltp > p.Ema20))
            return EntryVerdict.NoSignal("TrendStrategy", "1m EMA alignment / price below EMA20");
        if (p.Rsi5m is not { } rsi || (decimal)rsi < t.MinRsi5Floor || (decimal)rsi > t.MaxRsi5)
            return EntryVerdict.NoSignal("TrendStrategy", "5m RSI outside the band");
        if (p.AtrPct < t.MinAtrPct)
            return EntryVerdict.NoSignal("TrendStrategy", "ATR% below the setup minimum");
        if (t.RequireAbovePrev5mHigh && !(p.Prev5mHigh > 0 && p.LastClosedBar.Close > p.Prev5mHigh))
            return EntryVerdict.NoSignal("TrendStrategy", "1m close not above the previous 5m high");
        if (LiveGates.Has<IndexStopFloorGate>() && _settings.GetBool("EnableIndexStopFloor", p.Symbol, true) && (decimal)p.Atr < IndexStopFloorGate.MinAtrRaw(_trading))
            return EntryVerdict.GateBlock("IndexStopFloorGate", $"ATR {p.Atr:F2} pts below the index stop floor", path);
        return EntryVerdict.Pass(path);
    }

    /// <summary>The live EarlyTrendGate decision, plus the research exception (EntryRules.EarlyTrendMaxGapAtr) for a young, small 5m EMA gap.</summary>
    private string? EarlyTrendReason(MarketPoint p, string signalType, int side)
    {
        var reason = EarlyTrendGate.Evaluate(signalType, (decimal)p.Ema20_5m, (decimal)p.Ema50_5m, side);
        if (reason is not null && _rules.EarlyTrendMaxGapAtr is { } max && signalType == TradingConst.SignalType.CALL
            && p.Atr5m > 0 && (p.Ema20_5m - p.Ema50_5m) / p.Atr5m < max
            && (_rules.EarlyTrendMaxGapAtrFromMinute is not { } from || SessionClock.MinuteOfDayIst(p.TimeUtc) >= from)
            && (!_rules.EarlyTrendMaxGapAtrGapDaysOnly
                || ClassifyOpening(p, _rules.OpeningTypeThresholdPct) is OpeningType.GapUp or OpeningType.GapDown))
            return null;
        if (reason is not null && _rules.EarlyTrendGapUpMorningUntilMinute is { } until && signalType == TradingConst.SignalType.CALL
            && p.OpenGapPct is { } gap && gap >= _rules.GapMinPct && SessionClock.MinuteOfDayIst(p.TimeUtc) < until)
            return null;
        return reason;
    }

    private static bool FiveMinuteAgrees(MarketPoint p, TradeSide side) =>
        p.Ema20_5m > 0 && p.Ema50_5m > 0 && (side == TradeSide.Call ? p.Ema20_5m > p.Ema50_5m : p.Ema20_5m < p.Ema50_5m);

    /// <summary>Selector replay: null = Directional runs; otherwise the gate name and reason that suppress it.</summary>
    private (string By, string Reason)? SelectorBlock(MarketPoint p, TradeSide side, bool gapTrend)
    {
        if (_selectorMode != SelectorMode.Replay || p.RegimeVolatility is null || p.ReplayedMarketState is null)
            return null;

        if (p.RegimeVolatility is "Low" or "Extreme")
            return ("MarketRegimeGate", $"volatility {p.RegimeVolatility}");

        if (p.ReplayedMarketState is "Range" or "Reversal" or "NoTrade" or "News"
            && !(p.ReplayedMarketState is "Range" or "Reversal" && _rules.SelectorAllowStates?.Contains(p.ReplayedMarketState) == true))
            return (_rules.LabelSelectorState ? $"StrategySelector:{p.ReplayedMarketState}" : "StrategySelector",
                    $"MarketState {p.ReplayedMarketState} runs no engine");

        if (p.RegimeTrend == "Sideways")
        {
            var overridden = _rules.Sideways == SidewaysOverride.Always
                             || (_rules.Sideways == SidewaysOverride.When5mAgrees && FiveMinuteAgrees(p, side))
                             || (gapTrend && _rules.GapTrendSkipsSidewaysSuppression);
            if (!overridden)
                return ("StrategySelector", "Directional suppressed: MarketRegime is Sideways");
        }
        return null;
    }

    private static string? ProposedBlock(MarketPoint p, ProposedEntryRule r)
    {
        if (r.Opening != OpeningType.Any && ClassifyOpening(p, r.OpeningThresholdPct) != r.Opening)
            return $"session did not open {r.Opening}";
        if (p.Rsi1m < r.Rsi1mFloor || p.Rsi1m >= r.Rsi1mCeiling)
            return $"1m RSI {p.Rsi1m:F1} outside [{r.Rsi1mFloor:F0}, {r.Rsi1mCeiling:F0})";
        if (p.AtrPct < r.AtrPctMin || p.AtrPct >= r.AtrPctMax)
            return $"ATR {p.AtrPct:F4}% outside [{r.AtrPctMin}, {r.AtrPctMax})";
        if (r.Alignment != EmaAlignment.Any && AlignmentOf(p) != r.Alignment)
            return $"EMA alignment is {AlignmentOf(p)}, wants {r.Alignment}";
        return null;
    }

    private static EmaAlignment AlignmentOf(MarketPoint p) =>
        p.Ltp > p.Ema20 && p.Ema20 > p.Ema50 && p.Ema20_5m > p.Ema50_5m ? EmaAlignment.Bullish
        : p.Ltp < p.Ema20 && p.Ema20 < p.Ema50 && p.Ema20_5m < p.Ema50_5m ? EmaAlignment.Bearish
        : EmaAlignment.Mixed;

    /// <summary>
    /// The candidate replaces the AND-chain. Kept: the replayed selector, the index-stop floor, the early-trend gate and the no-trade window.
    /// Not applied: the live MinAtrPercent gate, because the candidate defines its own ATR window (several candidates sit below that minimum).
    /// </summary>
    private EntryVerdict EvaluateProposed(MarketPoint p, string signalType)
    {
        const string path = "PROPOSED_ENTRY";
        var side = signalType == TradingConst.SignalType.CALL ? TradeSide.Call : TradeSide.Put;
        if (SelectorBlock(p, side, gapTrend: false) is { } sel)
            return EntryVerdict.GateBlock(sel.By, sel.Reason, path);
        if (LiveGates.Has<IndexStopFloorGate>() && _settings.GetBool("EnableIndexStopFloor", p.Symbol, true) && (decimal)p.Atr < IndexStopFloorGate.MinAtrRaw(_trading))
            return EntryVerdict.GateBlock("IndexStopFloorGate", $"ATR {p.Atr:F2} pts < index stop floor ({p.Symbol})", path);
        if (LiveGates.Has<EarlyTrendGate>() && _settings.GetBool(EarlyTrendGate.EnableKey, p.Symbol, false))
        {
            var reason = EarlyTrendReason(p, signalType, _settings.GetInt(EarlyTrendGate.SideKey, p.Symbol, EarlyTrendGate.SideCallOnly));
            if (reason is not null)
                return EntryVerdict.GateBlock("EarlyTrendGate", reason, path);
        }
        if (LiveGates.Has<NoTradeWindowGate>() && _settings.GetBool(NoTradeWindowGate.EnableKey, p.Symbol, false)
            && NoTradeWindowGate.AppliesTo(_settings.GetInt(NoTradeWindowGate.SideKey, p.Symbol, NoTradeWindowGate.SideBoth), signalType))
        {
            var reason = NoTradeWindowGate.Evaluate(SessionClock.MinuteOfDayIst(p.TimeUtc),
                _settings.GetInt(NoTradeWindowGate.StartKey, p.Symbol, NoTradeWindowGate.DefaultStartMinute),
                _settings.GetInt(NoTradeWindowGate.EndKey, p.Symbol, NoTradeWindowGate.DefaultEndMinute));
            if (reason is not null)
                return EntryVerdict.GateBlock("NoTradeWindowGate", reason, path);
        }
        return EntryVerdict.Pass(path);
    }

    public EntryVerdict Evaluate(MarketPoint p, TradeSide side)
    {
        var symbol = p.Symbol;
        var signalType = side == TradeSide.Call ? TradingConst.SignalType.CALL : TradingConst.SignalType.PUT;

        if (_rules.RequireFastEmaAligned && p.Ema9 > 0 && p.Ema21 > 0
            && (side == TradeSide.Call ? p.Ema9 <= p.Ema21 : p.Ema9 >= p.Ema21))
            return EntryVerdict.NoSignal("FastEma", $"1m EMA9 {p.Ema9:F1} vs EMA21 {p.Ema21:F1} not aligned for {signalType}");

        // Stretch-at-entry restriction (research only; both null reproduces production).
        if (_rules.MaxStretchAtr is not null || _rules.MinStretchAtr is not null)
        {
            var stretch = StretchAtr(p, side);
            if (stretch is null)
                return EntryVerdict.NoSignal("Stretch", "ATR or EMA20 unavailable, stretch not computable");
            if (_rules.MaxStretchAtr is { } max && stretch > max)
            {
                // The cap may be conditional on the session looking range-bound. A trending session
                // is allowed its stretched continuation entries; a chopping one is not.
                var capApplies = true;
                if (_rules.CapOnlyWhenEfficiencyBelow is { } effMax)
                {
                    var eff = SessionEfficiency(p, _rules.EfficiencyMinRangeAtr);
                    capApplies = eff is null ? _rules.CapWhenEfficiencyUnknown : eff < effMax;
                }
                if (capApplies)
                    return EntryVerdict.NoSignal("Stretch", $"{stretch:F2} ATR beyond EMA20 > max {max:F2}");
            }
            if (_rules.MinStretchAtr is { } min && stretch < min)
                return EntryVerdict.NoSignal("Stretch", $"{stretch:F2} ATR beyond EMA20 < min {min:F2}");
        }

        // Opening-type restriction (research only; Any reproduces production). Applied before every path so a
        // dedicated trend strategy is restricted too.
        if (_rules.OpeningTypeFilter != OpeningType.Any)
        {
            var opening = ClassifyOpening(p, _rules.OpeningTypeThresholdPct);
            if (opening != _rules.OpeningTypeFilter)
                return EntryVerdict.NoSignal("OpeningType", opening is null
                    ? "no previous session close, opening type unknown"
                    : $"session opened {opening}, configuration wants {_rules.OpeningTypeFilter}");
        }

        // Candidate entry condition (research only; null reproduces production).
        if (_rules.Proposed is { } proposed)
        {
            if (ProposedBlock(p, proposed) is { } why)
                return EntryVerdict.NoSignal("ProposedEntry", why);
            if (proposed.ReplacesChain)
                return EvaluateProposed(p, signalType);
        }

        // ── rated direction (no RSI) replaces the chain for both sides ──────────────────────────────────────────────────
        if (_rules.Rated is { } rated)
            return EvaluateRated(p, side, signalType, rated);

        // ── a dedicated trend strategy replaces the chain for CALL ─────────────────────────────────────────────────────
        if (side == TradeSide.Call && _rules.Trend is { Strategy: not TrendStrategy.None } trend)
            return EvaluateTrend(p, trend);

        // ── the real CALL / PUT AND-chain ────────────────────────────────────────────────────────────────────────────
        var inputs = ToInputs(p, UseOneMinuteTrigger(symbol));
        var gapTrend = IsGapTrend(p, side);
        var engine = gapTrend && _gapEngine is not null ? _gapEngine : _engine;

        // Session trend: has the session actually travelled this way, without round-tripping?
        if (_rules.SessionTrendMinMoveAtr is { } minMove)
        {
            var opening = ClassifyOpening(p, _rules.SessionTrendGapPct);
            var inScope = _rules.SessionTrendAppliesTo == OpeningType.Any
                          || opening == _rules.SessionTrendAppliesTo;
            if (inScope)
            {
                var block = SessionTrendBlock(p, side, minMove);
                if (block is not null)
                    return EntryVerdict.NoSignal("SessionTrend", block);
            }
        }

        // Entry-time window (research only; null reproduces production).
        if (_rules.EntryFromMinuteIst is not null || _rules.EntryToMinuteIst is not null)
        {
            var minute = p.TimeIst.Hour * 60 + p.TimeIst.Minute;
            if (minute < (_rules.EntryFromMinuteIst ?? 0) || minute >= (_rules.EntryToMinuteIst ?? 24 * 60))
                return EntryVerdict.NoSignal("EntryWindow", $"{p.TimeIst:HH:mm} outside the configured window");
        }

        // Opening-conditional band: swap the RSI band (or refuse outright) on sessions that opened
        // with a gap in the configured direction. OpenGapPct is known at the open.
        if (_rules.OpeningBandAppliesTo != OpeningType.Any
            && ClassifyOpening(p, _rules.OpeningBandGapPct) == _rules.OpeningBandAppliesTo)
        {
            if (_rules.OpeningBandBlocks)
                return EntryVerdict.NoSignal("OpeningBand", $"side refused on {_rules.OpeningBandAppliesTo} sessions");
            if (_openingBandEngine is not null) engine = _openingBandEngine;
        }
        var verdict = side == TradeSide.Call ? engine.EvaluateCall(symbol, inputs) : engine.EvaluatePut(symbol, inputs);
        string path = "AND_CHAIN";
        if (verdict.Passed && _selectorMode == SelectorMode.UseStoredSnapshot
            && !string.Equals(p.StrategySelected, "Directional", StringComparison.OrdinalIgnoreCase))
        {
            return EntryVerdict.NoSignal("StrategySelector(stored)", $"stored StrategySelected = {p.StrategySelected ?? "no snapshot"}");
        }

        if (!verdict.Passed)
        {
            // PCR is joined from a snapshot; without one the PCR condition cannot be judged. Report it as missing data
            // rather than as a market block so coverage problems are visible in BlockedBy.
            var failed = verdict.Checks.Where(c => !c.Passed).Select(c => c.Label).ToList();
            if (p.Pcr is null && failed.Count > 0 && failed.All(l => l.StartsWith("12", StringComparison.Ordinal)))
                return EntryVerdict.NoSignal("DataUnavailable:Pcr", "no IndicatorSnapshots row to read PCR from");

            // Alternative path: the approved single-signal rule (BANKNIFTY PUT breakdown), evaluated only when the chain failed.
            if (side == TradeSide.Put && string.Equals(symbol, "BANKNIFTY", StringComparison.OrdinalIgnoreCase))
            {
                var match = _ruleEvaluator.Evaluate(ToRuleSnapshot(p));
                if (!match.Matched)
                    return EntryVerdict.NoSignal("AndChain", string.Join(" | ", failed));
                path = SingleSignalRuleEvaluator.BankNiftyPutBreakdownRuleId;
            }
            else
            {
                return EntryVerdict.NoSignal("AndChain", string.Join(" | ", failed));
            }
        }

        // ── live StrategyManager, after the approved-rule fallback: BreakoutProtection, then the 5m confirmation ──────────
        if (!_rules.SkipBreakoutProtection
            && _settings.GetBool(BreakoutProtectionService.EnableKey, symbol, false)
            && (side == TradeSide.Call ? p.BreakoutCall : p.BreakoutPut) is { Allowed: false } breakout)
            return EntryVerdict.GateBlock("BreakoutProtection", breakout.Reason, path);

        if (_settings.GetBool(FiveMinuteConfirmationService.EnabledSettingKey, symbol, false))
        {
            var confirmation = _fiveMinute.Confirm(side == TradeSide.Call, (decimal)p.Ema20_5m, (decimal)p.Ema50_5m,
                p.Rsi5m is { } r5 ? (decimal)r5 : null, _trading);
            if (!confirmation.Passed)
                return EntryVerdict.GateBlock("FiveMinuteConfirmation", $"BLOCKED_BY_5M_CONFIRMATION — {confirmation.Reason}", path);
        }

        // ── gap-up mode: counter-trend PUTs, then the CALL entry confirmation ───────────────────────────────────────────
        if (side == TradeSide.Put && _rules.GapUpBlocksPut != CounterTrendPut.Allow && IsGapUpMode(p)
            && (_rules.GapUpBlocksPut == CounterTrendPut.BlockChainAndRule || path == "AND_CHAIN"))
            return EntryVerdict.GateBlock("GapUpCounterTrendPut", "PUT blocked while the gap-up trend is active", path);
        if (side == TradeSide.Call && gapTrend && GapUpConfirm(p) is { } confirm)
            return EntryVerdict.GateBlock(confirm.By, confirm.Reason, path);

        // ── MarketRegimeGate and the strategy selector (replayed only in SelectorMode.Replay) ────────────────────────────
        if (path == "AND_CHAIN" && SelectorBlock(p, side, gapTrend) is { } sel)
        {
            // the selector ran no engine, so the live code falls to the approved rule (BANKNIFTY PUT) before giving up
            if (side == TradeSide.Put && string.Equals(symbol, "BANKNIFTY", StringComparison.OrdinalIgnoreCase)
                && sel.By == "StrategySelector" && _ruleEvaluator.Evaluate(ToRuleSnapshot(p)).Matched)
                path = SingleSignalRuleEvaluator.BankNiftyPutBreakdownRuleId;
            else
                return EntryVerdict.GateBlock(sel.By, sel.Reason, path);
        }

        // ── pre-gate: AtrGate (same switches and message as the live gate) ────────────────────────────────────────────
        if (_settings.GetBool("EnableAtrFilter", symbol, true) && _settings.IsSettingActive("MinAtrPercent", symbol))
        {
            var minAtr = _settings.GetDecimal("MinAtrPercent", symbol, 0.10m);
            if ((decimal)p.AtrPct < minAtr)
                return EntryVerdict.GateBlock("AtrGate", $"ATR {p.AtrPct:F4}% < minimum {minAtr:F4}% ({symbol})", path);
        }

        // ── post-gates that are pure functions of the point ──────────────────────────────────────────────────────────
        if (LiveGates.Has<IndexStopFloorGate>() && _settings.GetBool("EnableIndexStopFloor", symbol, true)
            && (decimal)p.Atr < IndexStopFloorGate.MinAtrRaw(_trading))
            return EntryVerdict.GateBlock("IndexStopFloorGate", $"ATR {p.Atr:F2} pts < index stop floor ({symbol})", path);

        if (LiveGates.Has<EarlyTrendGate>() && _settings.GetBool(EarlyTrendGate.EnableKey, symbol, false) && !(gapTrend && _rules.GapTrendSkipsEarlyTrendGate))
        {
            var sideSetting = _settings.GetInt(EarlyTrendGate.SideKey, symbol, EarlyTrendGate.SideCallOnly);
            var reason = EarlyTrendReason(p, signalType, sideSetting);
            if (reason is not null)
                return EntryVerdict.GateBlock("EarlyTrendGate", reason, path);
        }

        if (LivePostGateBlock(p, signalType) is { } post)
            return EntryVerdict.GateBlock(post.By, post.Reason, path);

        // NOTE: NoTradeWindowGate is NOT registered in the live SignalWorker.PostGates array, so production ignores
        // EnableNoTradeWindowGate. It is kept here because the existing research configurations rely on it; Program warns
        // when the setting is on so a parity run is not silently stricter than production.
        if (LiveGates.Has<NoTradeWindowGate>() && _settings.GetBool(NoTradeWindowGate.EnableKey, symbol, false))
        {
            var sideSetting = _settings.GetInt(NoTradeWindowGate.SideKey, symbol, NoTradeWindowGate.SideBoth);
            if (NoTradeWindowGate.AppliesTo(sideSetting, signalType))
            {
                var start = _settings.GetInt(NoTradeWindowGate.StartKey, symbol, NoTradeWindowGate.DefaultStartMinute);
                var end = _settings.GetInt(NoTradeWindowGate.EndKey, symbol, NoTradeWindowGate.DefaultEndMinute);
                var reason = NoTradeWindowGate.Evaluate(SessionClock.MinuteOfDayIst(p.TimeUtc), start, end);
                if (reason is not null)
                    return EntryVerdict.GateBlock("NoTradeWindowGate", reason, path);
            }
        }

        return EntryVerdict.Pass(path);
    }

    /// <summary>
    /// Builds the live engine's input record from the rebuilt point (delta/OI/volume/VWAP are not part of the active rules).
    /// Live parity (MarketSnapshotBuilder): DayOpen/DayLow/DayHigh are the SESSION values (the tick's session open/low/high),
    /// not the drifting first bar of the 200-bar window; DayChangePct is measured from the previous session's close;
    /// PriceChangePct keeps the drifting window open exactly as production stores it. Points built by hand (tests) that do not
    /// carry the session fields fall back to the old per-point values.
    /// </summary>
    internal static SignalEngineService.EntryMarketInputs ToInputs(MarketPoint p, bool oneMinuteTrigger = true)
    {
        var dayOpen = p.SessionOpen > 0 ? p.SessionOpen : p.DayOpen;
        var dayLow = p.SessionLow > 0 && p.SessionLow < double.MaxValue ? p.SessionLow : p.DayLow;
        var dayHigh = p.SessionHigh > 0 ? p.SessionHigh : p.DayHigh;
        var dayChangePct = p.PrevClose is { } pc && pc > 0 ? (p.Ltp - pc) / pc * 100.0 : p.DayChangePct;
        var yesterdayClose = p.PrevClose is { } prev && prev > 0
            ? (decimal)prev
            // Recovered exactly from the gap the series already carries: OpenGapPct was computed as
            // (SessionOpen - prevClose) / prevClose * 100. 0 when the first session has no previous close,
            // which disables the opening-conditional RSI band for that session.
            : p.OpenGapPct is { } g && Math.Abs(1 + g / 100.0) > 1e-9
                ? (decimal)(p.SessionOpen / (1 + g / 100.0))
                : 0m;

        // live StrategyManager.ResolveTriggerCandle: the last closed 1m bar, or today's last closed 5m bar (null before one closes)
        var trigger = oneMinuteTrigger ? p.LastClosedBar : p.LastClosed5mToday;

        return new(
            Ltp: (decimal)p.Ltp, Ema20: (decimal)p.Ema20, Ema50: (decimal)p.Ema50,
            Rsi: (decimal)p.Rsi1m, Rsi5m: p.Rsi5m is { } r ? (decimal)r : null,
            Pcr: (decimal)(p.Pcr ?? 0), AtrPct: (decimal)p.AtrPct, PriceChangePct: (decimal)p.DayChangePct,
            CurrentVolume: 0, AvgVolume20: 0, Vwap: 0, Prev5mHigh: (decimal)p.Prev5mHigh, Prev5mLow: (decimal)p.Prev5mLow,
            DayOpen: (decimal)dayOpen, DayLow: (decimal)dayLow, DayHigh: (decimal)dayHigh, DayChangePct: (decimal)dayChangePct,
            YesterdayClose: yesterdayClose,
            CeAtmDelta: 0, PeAtmDelta: 0, CeChangeInOi: 0, PeChangeInOi: 0,
            TrendBullish: p.Ema20_5m > 0 && p.Ema50_5m > 0 && p.Ema20_5m > p.Ema50_5m,
            TrendBearish: p.Ema20_5m > 0 && p.Ema50_5m > 0 && p.Ema20_5m < p.Ema50_5m,
            LastClosedCandle: trigger is { } b
                ? new Candle
                {
                    Symbol = p.Symbol, Open = (decimal)b.Open, High = (decimal)b.High,
                    Low = (decimal)b.Low, Close = (decimal)b.Close, CandleTime = b.OpenUtc,
                }
                : null);
    }

    /// <summary>Live StrategyManager.UseOneMinuteTrigger for the replayed mode; no mode = the 1m bar.</summary>
    private bool UseOneMinuteTrigger(string symbol) =>
        _tradingMode is not { } mode || global::Trading.Worker.StrategyManager.UseOneMinuteTrigger(mode, _settings, symbol);

    /// <summary>
    /// The live post-gates that are pure functions of the point, in SignalWorker.PostGates order after EarlyTrendGate:
    /// ChopGate, FastEmaGate, SessionTrendGate. Each reads the same settings rows and calls the gate's own Evaluate.
    /// VwapSideGate is a no-op in production (index VWAP is always null). CrossIndexAgreementGate, MarketDirectionGate,
    /// PutS1Gate and StaleEntryGate need data the replay does not carry; Program warns when they are enabled.
    /// </summary>
    private (string By, string Reason)? LivePostGateBlock(MarketPoint p, string signalType)
    {
        var symbol = p.Symbol;
        var dayOpen = (decimal)(p.SessionOpen > 0 ? p.SessionOpen : p.DayOpen);
        var yesterdayClose = p.PrevClose is { } pc ? (decimal)pc : 0m;

        if (LiveGates.Has<ChopGate>() && _settings.GetBool(ChopGate.EnableKey, symbol, false) && p.Chop15 is { } chop && (decimal)chop > ChopGate.MaxChop)
            return ("ChopGate", $"Chop gate: 15-bar chop {chop:F2} > {ChopGate.MaxChop} ({symbol})");

        if (LiveGates.Has<FastEmaGate>() && _settings.GetBool(FastEmaGate.EnableKey, symbol, false)
            && !(_settings.GetBool(FastEmaGate.GapUpOnlyKey, symbol, true)
                 && !FastEmaGate.OpenedGapUp(dayOpen, yesterdayClose, _settings.GetDecimal(FastEmaGate.GapPctKey, symbol, 0.30m)))
            && FastEmaGate.Evaluate(signalType, (decimal)p.Ema9, (decimal)p.Ema21,
                   _settings.GetInt(FastEmaGate.SideKey, symbol, FastEmaGate.SideCallOnly)) is { } fast)
            return ("FastEmaGate", $"{fast} ({symbol})");

        if (LiveGates.Has<SessionTrendGate>() && _settings.GetBool(SessionTrendGate.EnableKey, symbol, false))
        {
            var dayLow = (decimal)(p.SessionLow > 0 && p.SessionLow < double.MaxValue ? p.SessionLow : p.DayLow);
            var dayHigh = (decimal)(p.SessionHigh > 0 ? p.SessionHigh : p.DayHigh);
            var reason = SessionTrendGate.Evaluate(
                signalType, (decimal)p.Ltp, dayOpen, dayHigh, dayLow, (decimal)p.Atr, yesterdayClose,
                minMoveAtr: _settings.GetDecimal(SessionTrendGate.MinMoveKey, symbol, 0.75m),
                minEfficiency: _settings.GetDecimal(SessionTrendGate.MinEfficiencyKey, symbol, 0.50m),
                nearExtremeAtr: _settings.GetDecimal(SessionTrendGate.NearExtremeKey, symbol, 0m),
                opening: _settings.GetInt(SessionTrendGate.OpeningKey, symbol, SessionTrendGate.OpeningAny),
                gapPct: _settings.GetDecimal(SessionTrendGate.GapPctKey, symbol, 0.30m),
                side: _settings.GetInt(SessionTrendGate.SideKey, symbol, SessionTrendGate.SideBoth));
            if (reason is not null)
                return ("SessionTrendGate", $"{reason} ({symbol})");
        }

        return null;
    }

    /// <summary>The row the live rule reads (StrategyManager.BuildRuleSnapshot); MarketState / TrendDirection come from the stored snapshot only.</summary>
    internal static IndicatorSnapshot ToRuleSnapshot(MarketPoint p) => new()
    {
        Symbol = p.Symbol,
        TrendDirection = p.TrendDirection ?? (p.Ema20 > p.Ema50 ? "Bullish" : p.Ema20 < p.Ema50 ? "Bearish" : "Sideways"),
        MarketState = p.ReplayedMarketState ?? p.MarketState ?? "",
        Ltp = (decimal)p.Ltp, Ema20 = (decimal)p.Ema20, Ema50 = (decimal)p.Ema50,
        Ema20_5m = (decimal)p.Ema20_5m, Ema50_5m = (decimal)p.Ema50_5m,
        Rsi = (decimal)p.Rsi1m, Rsi5m = p.Rsi5m is { } r ? (decimal)r : null,
        AtrRaw = (decimal)p.Atr, AtrPct = (decimal)p.AtrPct, Pcr = (decimal)(p.Pcr ?? 0),
        PriceChangePct = (decimal)p.DayChangePct,
    };
}
