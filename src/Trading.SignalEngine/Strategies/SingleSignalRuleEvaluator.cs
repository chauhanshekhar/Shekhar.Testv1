using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Strategies;

/// <summary>Outcome of a single-signal rule evaluation.</summary>
/// <param name="Matched">True only when every condition of the rule held.</param>
/// <param name="RuleId">Identifier of the rule that matched — null when nothing matched.</param>
/// <param name="OptionType">CE or PE for the matched rule — null when nothing matched.</param>
/// <param name="Call">True for a CALL, false for a PUT.</param>
/// <param name="Reason">
/// The FIRST condition that failed, for Debug-level suppression logging. Null on a match.
/// First-failure rather than a full list so the log line stays one short string per cycle.
/// </param>
public readonly record struct SingleSignalRuleResult(
    bool Matched,
    string? RuleId,
    string? OptionType,
    bool Call,
    string? Reason)
{
    public static SingleSignalRuleResult NoMatch(string reason) => new(false, null, null, false, reason);

    public static SingleSignalRuleResult ForPut(string ruleId) =>
        new(true, ruleId, TradingConst.OptionType.PE, false, null);

    public static SingleSignalRuleResult ForCall(string ruleId) =>
        new(true, ruleId, TradingConst.OptionType.CE, true, null);
}

/// <summary>
/// Historically validated, hand-approved single-signal setups that are evaluated INDEPENDENTLY of
/// SignalEngineService's combined AND-chain and of the 70-confidence floor in StrategyManager.
///
/// Each rule here is a literal transcription of a backtested condition set — nothing is added to
/// it (no confidence floor, no delta/PCR/MarketRegime requirement), because adding a condition
/// that was not in the backtest invalidates the measured win rate. Rules are evaluated against
/// <see cref="IndicatorSnapshot"/>, the same row shape the historical validation runs over, so the
/// live rule and the backtest read identical fields.
///
/// This evaluator decides ELIGIBILITY ONLY. Every existing protection — pre-gates, duplicate/
/// cooldown checks, open-position and risk gates, lot sizing, option selection, SL/T1/T2 — still
/// runs afterwards, unchanged.
/// </summary>
public class SingleSignalRuleEvaluator
{
    public const string BankNiftyPutBreakdownRuleId = "BANKNIFTY_PUT_BREAKDOWN";

    private const string BankNiftySymbol = "BANKNIFTY";
    private const string BearishTrend = "Bearish";
    private const string BreakdownState = "Breakdown";

    // ── BANKNIFTY PUT BREAKDOWN thresholds ──────────────────────────────────────────────────
    // Backtest: 448 samples — 5m 73.66% / 10m 88.17% / 15m 94.64% win rate;
    // avg move +16.05 / +20.41 / +28.11 pts; worst 15m -22.25, best 15m +81.75.
    // Deliberately hardcoded rather than read from StrategySettings: these are the exact values
    // the win rate was measured at, and a runtime override would silently invalidate it. Changing
    // any of them requires a fresh backtest.
    private const decimal RsiMaximum = 35m;
    private const decimal PriceChangePctMaximum = -0.20m;
    private const decimal AtrPctMinimum = 0.035m;

    /// <summary>
    /// Evaluates every approved rule and returns the first match. Only the BANKNIFTY PUT
    /// BREAKDOWN rule is approved today — the other candidates from the same basket
    /// (BANKNIFTY PUT_STRONG_TREND, NIFTY PUT/CALL_STRONG_TREND, BANKNIFTY CALL_BREAKOUT,
    /// SENSEX PUT_STRONG_TREND) were NOT approved and are intentionally absent.
    /// </summary>
    private readonly IStrategySettingsService? _settings;

    /// <summary>
    /// DB-backed kill switch for the BANKNIFTY PUT BREAKDOWN rule, DEFAULTING TO ENABLED so
    /// deploying this changes nothing until a StrategySettings row says otherwise.
    ///
    /// WHY THIS EXISTS: the rule's thresholds are deliberately hardcoded (see above) so a runtime
    /// override cannot silently invalidate the measured win rate. That reasoning still holds for
    /// the THRESHOLDS, but it left the rule itself with no off switch at all — disabling it meant
    /// a code change and a redeploy, which is the wrong thing to need in a hurry on a live account.
    /// Turning it off: INSERT/UPDATE StrategySettings SET SettingValue = 0 for this key. Picked up
    /// within the settings cache's 5-minute refresh — no restart.
    /// </summary>
    public const string EnabledKey = "EnableBankNiftyPutBreakdownRule";

    public SingleSignalRuleEvaluator() { }

    public SingleSignalRuleEvaluator(IStrategySettingsService settings) => _settings = settings;

    /// <summary>
    /// True unless a StrategySettings row explicitly disables it. Null settings (the parameterless
    /// constructor used by tests) means enabled, so existing behaviour is unchanged.
    /// </summary>
    public bool IsBankNiftyRuleEnabled() =>
        _settings?.GetBool(EnabledKey, BankNiftySymbol, true) ?? true;

    /// <remarks>
    /// Single entry point for every approved rule; first match wins. Each rule guards on its own
    /// symbol, so they can never contend. BANKNIFTY PUT BREAKDOWN is the only approved rule —
    /// NIFTY and SENSEX reach none, and fall through to the AND-chain like any other symbol.
    /// </remarks>
    public SingleSignalRuleResult Evaluate(
        IndicatorSnapshot s, bool entryCandleBullish = false, bool entryCandleBearish = false)
    {
        // The kill switch gates ELIGIBILITY only — EvaluateBankNiftyPutBreakdown itself stays pure
        // and unconditional so its tests, and any direct caller, still see the raw rule verdict.
        return IsBankNiftyRuleEnabled()
            ? EvaluateBankNiftyPutBreakdown(s)
            : SingleSignalRuleResult.NoMatch($"BANKNIFTY PUT BREAKDOWN disabled ({EnabledKey}=0)");
    }

    /// <summary>
    /// BANKNIFTY PUT BREAKDOWN. Conditions are checked in the declared order so the suppression
    /// reason names the first one that failed.
    /// </summary>
    public SingleSignalRuleResult EvaluateBankNiftyPutBreakdown(IndicatorSnapshot s)
    {
        if (!string.Equals(s.Symbol, BankNiftySymbol, StringComparison.OrdinalIgnoreCase))
            return SingleSignalRuleResult.NoMatch($"Symbol is {s.Symbol}, not {BankNiftySymbol}");

        if (!string.Equals(s.TrendDirection, BearishTrend, StringComparison.OrdinalIgnoreCase))
            return SingleSignalRuleResult.NoMatch($"TrendDirection is {s.TrendDirection}, not {BearishTrend}");

        if (!string.Equals(s.MarketState, BreakdownState, StringComparison.OrdinalIgnoreCase))
            return SingleSignalRuleResult.NoMatch($"MarketState is {s.MarketState}, not {BreakdownState}");

        if (!(s.Ema20_5m < s.Ema50_5m))
            return SingleSignalRuleResult.NoMatch($"Ema20_5m {s.Ema20_5m:F2} not below Ema50_5m {s.Ema50_5m:F2}");

        if (!(s.Ltp < s.Ema20))
            return SingleSignalRuleResult.NoMatch($"Ltp {s.Ltp:F2} not below Ema20 {s.Ema20:F2}");

        if (!(s.Ema20 < s.Ema50))
            return SingleSignalRuleResult.NoMatch($"Ema20 {s.Ema20:F2} not below Ema50 {s.Ema50:F2}");

        if (!(s.Rsi <= RsiMaximum))
            return SingleSignalRuleResult.NoMatch($"RSI {s.Rsi:F2} above {RsiMaximum}");

        if (!(s.PriceChangePct <= PriceChangePctMaximum))
            return SingleSignalRuleResult.NoMatch($"PriceChangePct {s.PriceChangePct:F4} above {PriceChangePctMaximum}");

        if (!(s.AtrPct >= AtrPctMinimum))
            return SingleSignalRuleResult.NoMatch($"AtrPct {s.AtrPct:F4} below {AtrPctMinimum}");

        return SingleSignalRuleResult.ForPut(BankNiftyPutBreakdownRuleId);
    }
}
