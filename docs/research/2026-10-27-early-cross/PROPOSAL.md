# Proposal: paper-only early-entry CALL (BANKNIFTY), off by default

STATUS: PROPOSED, NOT APPLIED. No file under `src/` (outside `Trading.Backtest`) has been edited.

## Problem
Live CALLs enter late in the move; entries made 16+ minutes after the 1m EMA20 rose above EMA50 lost money, entries in the
first ~15 minutes made money (replay, after costs). See `docs/research/2026-10-27-late-entry-leave-one-out/`.

## Evidence (replay, `--early-cross`, Rs20/order + 1pt, BANKNIFTY CALL, 62 sessions)
| Arm | Trades | Net after costs | Win | PF | Train / Val / Test |
|---|---|---|---|---|---|
| Live rules | 20 | +2,082 | 45% | 1.3 | 0 / -11,109 / +20,191 |
| Within 10 min, no filters | 194 | -78,013 | 34% | 1.0 | -18,833 / -6,765 / +13,805 |
| Within 10 min + EMA gap | 56 | +30,547 | 59% | 2.0 | +26,245 / -1,223 / +25,546 |
| Within 10 min + 5m not up + EMA gap (**proposed**) | 41 | +38,789 | 68% | 2.8 | +17,266 / -1,223 / +38,006 |
Without best day +27,836; without best 3 days +13,978; 18 of 29 days positive; only 12 of 41 trades on real option prices.
Picked from 8 arms after viewing results; validation slice is 8 trades. NOT live-validated, hence paper only.

## Root cause
No live rule looks at how old the 1m up-move is. The chain's only timing-sensitive pieces (selector state, early-trend gate,
PCR) either hold early minutes back or let late ones through (see research folder).

## Design
Reuse the existing flag-gated rule pattern (`GeminiGapRuleEvaluator`, built inside `SingleSignalRuleEvaluator`): the rule only
decides ELIGIBILITY; option leg, SL/T1/T2, lot sizing, every pre/post gate, duplicate protection, TradeJournal and the exit
flow still run via `GenerateRuleSignal` exactly as for the approved BANKNIFTY PUT rule.
- Flag `EnableEarlyCrossCall` (StrategySettings, BANKNIFTY row only, default off; absent row = off).
- **Paper-only is enforced in code**, not by convention: the rule returns no match unless `ctx.Orders.CurrentMode == TradingMode.Paper`
  (Live and HeroZero never match).
- Conditions (all from the arm that was tested): 1m EMA20 > EMA50 for at most `EarlyCrossMaxBars` (10) closed bars since the cross;
  LTP > EMA20; last closed 1m bar bullish (close > open, no body% floor, as tested); 5m RSI inside the existing
  `CallRsiFloor`..`CallRsiCeiling` band (fails closed when null); 5m trend not yet up (5m EMA20 <= EMA50, when `EarlyCrossRequire5mNotUp`=1);
  |EMA20-EMA50| > `EmaGapMinPoints` for the symbol (when `EarlyCrossRequireEmaGap`=1).
- No new logging: the existing `Debug` suppression line pattern is reused, and the signal is tagged `SelectedStrategy = "EarlyCross"`
  (no schema change) so paper trades can be separated, as the Gemini rules do.

## Files
1. NEW `src/Trading.SignalEngine/Strategies/EarlyCrossCallRuleEvaluator.cs`
2. EDIT `src/Trading.SignalEngine/Strategies/SingleSignalRuleEvaluator.cs` (construct + two pass-through members, same as `_gapRules`)
3. EDIT `src/Trading.Worker/StrategyManager.cs` (call before the Range block, one new private method, strategy tag)
4. NEW `src/Trading.Tests/EarlyCrossCallRuleEvaluatorTests.cs`
5. NEW (not executed) `docs/set-banknifty-early-cross-paper.sql` - guarded insert of the 4 BANKNIFTY rows with ROLLBACK and VERIFY blocks

## Diff

### 1. New file `EarlyCrossCallRuleEvaluator.cs`
```csharp
using Trading.Application.Interfaces;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Strategies;

/// <summary>Everything <see cref="EarlyCrossCallRuleEvaluator"/> reads; built once per cycle by StrategyManager.</summary>
public readonly record struct EarlyCrossInput(
    bool IsPaper,
    int? BarsSince1mUp,
    decimal Ltp,
    decimal Ema20,
    decimal Ema50,
    decimal Ema20_5m,
    decimal Ema50_5m,
    decimal? Rsi5m,
    bool LastBarBullish);

/// <summary>
/// CALL entry in the first minutes after the 1m EMA20 rose above EMA50 (buy the turn, not the established move).
/// UNVALIDATED LIVE: backed only by a replay (docs/research/2026-10-27-early-cross). Therefore OFF by default
/// (EnableEarlyCrossCall) and PAPER ONLY, enforced here: any other TradingMode returns no match.
/// Decides ELIGIBILITY ONLY, like <see cref="SingleSignalRuleEvaluator"/>: option leg, SL/T1/T2, lot sizing, every gate,
/// duplicate protection and the exit flow still run.
/// </summary>
public class EarlyCrossCallRuleEvaluator
{
    public const string RuleId = "EARLY_CROSS_CALL";
    /// <summary>Written to Signal.SelectedStrategy so these trades can be separated without a schema change.</summary>
    public const string StrategyTag = "EarlyCross";

    public const string EnableKey = "EnableEarlyCrossCall";
    public const string MaxBarsKey = "EarlyCrossMaxBars";
    public const string Require5mNotUpKey = "EarlyCrossRequire5mNotUp";
    public const string RequireEmaGapKey = "EarlyCrossRequireEmaGap";
    public const int DefaultMaxBars = 10;

    private readonly IStrategySettingsService _settings;

    public EarlyCrossCallRuleEvaluator(IStrategySettingsService settings) => _settings = settings;

    public bool IsEnabled(string symbol) => _settings.GetBool(EnableKey, symbol, false);

    public SingleSignalRuleResult Evaluate(string symbol, in EarlyCrossInput e)
    {
        if (!IsEnabled(symbol))
            return SingleSignalRuleResult.NoMatch("EarlyCross disabled");
        if (!e.IsPaper)
            return SingleSignalRuleResult.NoMatch("EarlyCross is paper-only");

        var maxBars = _settings.GetInt(MaxBarsKey, symbol, DefaultMaxBars);
        if (e.BarsSince1mUp is not { } bars || bars > maxBars)
            return SingleSignalRuleResult.NoMatch("EarlyCross: 1m trend did not turn up within the allowed minutes");
        if (!(e.Ltp > e.Ema20 && e.Ema20 > e.Ema50))
            return SingleSignalRuleResult.NoMatch("EarlyCross: price/EMA alignment");
        if (!e.LastBarBullish)
            return SingleSignalRuleResult.NoMatch("EarlyCross: last 1m bar is not bullish");

        // Same band the CALL chain uses (condition 5); null RSI fails closed.
        var floor = _settings.GetDecimal("CallRsiFloor", symbol, 45m);
        var ceiling = _settings.GetDecimal("CallRsiCeiling", symbol, 70m);
        if (e.Rsi5m is not { } rsi || rsi < floor || rsi > ceiling)
            return SingleSignalRuleResult.NoMatch("EarlyCross: 5m RSI outside the CALL band");

        if (_settings.GetBool(Require5mNotUpKey, symbol, true)
            && e.Ema20_5m > 0 && e.Ema50_5m > 0 && e.Ema20_5m > e.Ema50_5m)
            return SingleSignalRuleResult.NoMatch("EarlyCross: 5m trend already up");

        if (_settings.GetBool(RequireEmaGapKey, symbol, true)
            && e.Ema20 - e.Ema50 < _settings.GetDecimal("EmaGapMinPoints", symbol, 10m))
            return SingleSignalRuleResult.NoMatch("EarlyCross: EMA gap below the minimum");

        return SingleSignalRuleResult.ForCall(RuleId);
    }

    /// <summary>
    /// Closed 1m bars since EMA20 first rose above EMA50 (0 = first bar above), from the closed candles in the snapshot.
    /// Null when EMA20 is not above EMA50 now, or the run covers the whole window (start unknown, never "early").
    /// Incremental EMAs over the window: within a small tolerance of the production EMA, which is why the early window is
    /// a count of bars, not a price level.
    /// </summary>
    public static int? BarsSince1mUp(IReadOnlyList<Candle> candles1m)
    {
        if (candles1m.Count < 60) return null;
        var a20 = 2m / 21m; var a50 = 2m / 51m;
        var e20 = candles1m[0].Close; var e50 = candles1m[0].Close;
        var run = 0; var runStartsAtWindowStart = true;
        for (var i = 1; i < candles1m.Count; i++)
        {
            e20 += a20 * (candles1m[i].Close - e20);
            e50 += a50 * (candles1m[i].Close - e50);
            if (e20 > e50) run++; else { run = 0; runStartsAtWindowStart = false; }
        }
        return run == 0 || runStartsAtWindowStart ? null : run - 1;
    }
}
```

### 2. `SingleSignalRuleEvaluator.cs`
```diff
     private readonly GeminiGapRuleEvaluator? _gapRules;
+    private readonly EarlyCrossCallRuleEvaluator _earlyCross;
 ...
         _gapRules = new GeminiGapRuleEvaluator(settings);
+        _earlyCross = new EarlyCrossCallRuleEvaluator(settings);
 ...
     public bool IsGeminiGapEnabled(string symbol) => _gapRules?.AnyRuleEnabled(symbol) ?? false;
+
+    public bool IsEarlyCrossEnabled(string symbol) => _earlyCross.IsEnabled(symbol);
+
+    public SingleSignalRuleResult EvaluateEarlyCross(string symbol, in EarlyCrossInput input) =>
+        _earlyCross.Evaluate(symbol, input);
```

### 3. `StrategyManager.cs`
```diff
         var decision = await _selector.Select(strategyContext);
 
+        // Early-cross CALL (paper only, off by default): evaluated BEFORE the Range block so it can act in the first minutes of
+        // a turn the selector still labels Range/Reversal. Returns null in every other case (flag off, not Paper, no match).
+        var earlySignal = TryGenerateEarlyCrossSignal(ctx, market, symbol, out var earlyRuleId);
+
         // Range = no clear trend: ...
-        if (ShouldBlockRangeMarket(decision.MarketState, ctx.StrategySettings.GetBool("BlockRangeMarketState", symbol, true)))
+        if (earlySignal is null && ShouldBlockRangeMarket(decision.MarketState, ctx.StrategySettings.GetBool("BlockRangeMarketState", symbol, true)))
         {
 ...
         var signal = engineResult.Signal;
         string? ruleId = null;
+        if (signal == null && earlySignal != null)
+        {
+            signal = earlySignal;
+            ruleId = earlyRuleId;
+        }
 ...
-        signal.SelectedStrategy = GeminiGapRuleEvaluator.IsGeminiRule(ruleId)
-            ? GeminiGapRuleEvaluator.StrategyTag
-            : decision.Strategy.ToString();
+        signal.SelectedStrategy = GeminiGapRuleEvaluator.IsGeminiRule(ruleId)
+            ? GeminiGapRuleEvaluator.StrategyTag
+            : ruleId == EarlyCrossCallRuleEvaluator.RuleId
+                ? EarlyCrossCallRuleEvaluator.StrategyTag
+                : decision.Strategy.ToString();
 ...
+    // ── Early-cross CALL (paper only) ─────────────────────────────────────────────────────────
+    // One settings lookup per cycle when the flag is off (the default). Uses the last closed 1m bar as the trigger on purpose:
+    // the default trigger candle in Paper is the 5m bar, but the tested rule is a 1m-turn rule.
+    private Signal? TryGenerateEarlyCrossSignal(SignalContext ctx, MarketSnapshot market, string symbol, out string? ruleId)
+    {
+        ruleId = null;
+        if (!_ruleEvaluator.IsEarlyCrossEnabled(symbol))
+            return null;
+
+        var bar = market.LastClosed1mCandle;
+        var input = new EarlyCrossInput(
+            ctx.Orders?.CurrentMode == TradingMode.Paper,
+            EarlyCrossCallRuleEvaluator.BarsSince1mUp(market.Candles1m),
+            market.Ltp, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m, market.Rsi5m,
+            bar is not null && bar.Close > bar.Open);
+
+        var match = _ruleEvaluator.EvaluateEarlyCross(symbol, input);
+        if (!match.Matched)
+        {
+            _logger.LogDebug("EARLY CROSS SUPPRESSED Symbol={Symbol} Reason={Reason}", symbol, match.Reason);
+            return null;
+        }
+
+        var result = ctx.Engine.GenerateRuleSignal(
+            symbol, match.OptionType!, match.Call, market.Ltp, market.Ema20, market.Ema50, market.PcrValue, market.Rsi,
+            market.CurrentVolume, market.AvgVolume20, market.AtrRaw, market.OptionChain, bar, out _);
+
+        if (result.Signal == null)
+            return null;
+
+        ruleId = match.RuleId;
+        return result.Signal;
+    }
```
(The `GetInt`/`GetBool` calls follow the existing `IStrategySettingsService` API used by `GeminiGapRuleEvaluator`; hunk context lines are matched on apply.)

### 4. Tests `EarlyCrossCallRuleEvaluatorTests.cs` (xUnit, next to `GeminiGapRuleEvaluatorTests.cs`)
Flag off -> no match; flag on but not Paper (Live, HeroZero) -> no match; bars > max -> no match; bars null -> no match; price below EMA20;
bearish bar; RSI below floor / above ceiling / null; 5m already up (and allowed when `EarlyCrossRequire5mNotUp`=0); EMA gap below
min (and allowed when `EarlyCrossRequireEmaGap`=0); full match returns `EARLY_CROSS_CALL` with CE; `BarsSince1mUp` unit cases
(fresh cross returns 0, 7 bars after returns 7, down trend null, run covering whole window null, fewer than 60 candles null).

### 5. `docs/set-banknifty-early-cross-paper.sql` (written after approval, never executed by Claude)
Idempotent `IF EXISTS ... UPDATE ELSE INSERT` for BANKNIFTY rows `EnableEarlyCrossCall`=1, `EarlyCrossMaxBars`=10,
`EarlyCrossRequire5mNotUp`=1, `EarlyCrossRequireEmaGap`=1, with a commented ROLLBACK block (set `EnableEarlyCrossCall`=0 / delete rows) and a VERIFY query.
No global (`Symbol IS NULL`) row is created, so other symbols stay off.

## Risk
- Paper only by code guard, off by default; zero behaviour change until the BANKNIFTY row exists AND mode is Paper.
- Replay vs paper will differ: post-gates still apply on this path (OiConfirmationGate has been seen holding setups 10-100 min and
  can defeat the early timing; also OnePosition, Duplicate, Risk, IndexStopFloor, StaleEntry, EarlyTrendGate (passes by construction
  when 5m-not-up is required), Session, CrossIndex, VWAP, Handoff, R:R, Spread). Differences are the point of the paper run.
- `BarsSince1mUp` uses incremental EMAs over the snapshot window; the replay used production-style EMAs. Expect small timing differences.
- Rule bypasses PCR, the selector and the Range block for these signals only; Live/HeroZero cannot reach it.
- Does not touch exits, order placement, journal, position sync or any existing gate.

## Validation
`dotnet restore`, `dotnet build TradingPlatform.slnx`, `dotnet test src/Trading.Tests` (new tests plus existing). Deployment checks
(Docker build, API/workers start, SQL/Groww/SignalR connect) are listed for you; nothing is deployed by Claude.

## Paper evaluation plan (before any live discussion)
Run at least 15 sessions / 30 trades. Compare against the replay (about 0.7 trades per session, +38.8k after costs on 41 trades) using
paper fills, `SelectedStrategy = 'EarlyCross'`, costs applied. Stop and revert the flag if net after costs is negative at 20 trades.

## Deployment impact
None until approved, deployed by you, and the BANKNIFTY row is inserted by you. Rollback = set `EnableEarlyCrossCall` to 0.
