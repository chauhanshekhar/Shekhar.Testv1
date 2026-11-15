# Proposal: SENSEX two-read stop - SHADOW TEST ONLY (2026-11-11)

Status: PROPOSED. Nothing implemented. Needs explicit approval before any change under `src/`.
Evidence: `docs/research/2026-11-11-option-quote-noise/` (replay.py, ranking.md).

## Problem
SENSEX option stops fire on one-minute quote noise (median noise 1.4% of premium, p90 4.0%; stops are 3.7-5%).
Signals 10348/10349 (2026-11-11) were stopped within 40 s on a flat index.

## Hypothesis
A SENSEX stop that needs two consecutive completed 1-minute premium readings at or below the stop
(`<=`, same convention as `triggerLtp <= signal.OptionSL`) beats the current single-read stop on net points,
profit factor and expectancy per trade, in both halves of the test period.
Replay (196 signals, minute data): SENSEX +91 vs -149 pts, PF 1.07 vs 0.89. 90% CI of the paired day gain +25..+497;
SENSEX PUT alone CI -38..+424 (not significant). Not proven.

## Scope
SENSEX only, CALL and PUT reported separately. NIFTY/BANKNIFTY untouched (control group showed no consistent gain).
The actual stop (broker SL_M / paper stop) is NEVER delayed, modified or cancelled.

## Recommended design: two phases

### Phase 1 - offline shadow report (no change under `src/`, no schema change)
A read-only script (`docs/research/<date>-two-read-shadow/shadow_report.py`) run after each session. Inputs already stored:
`Signals` (ExitTime, ExitReason, OptionSL stop distance, original stop rebuilt from index stop x delta where OptionSL was moved to entry),
`TradeJournal` (actual P&L), `OptionMinuteHistory` (completed 1-minute premiums).
For every SENSEX signal whose actual exit was a stop (`OPTION_SL_HIT`, not a profit lock):
1. First breach = actual exit time/price.
2. Second reading = first OptionMinuteHistory row with MinuteTime >= the next minute boundary after the breach.
3. At or below the stop -> hypothetical confirmed exit at that reading's Ltp.
   Above the stop -> hypothetical cancelled exit; the position continues and the same rule is applied each following minute
   until T1, a confirmed stop, or 15:15.
4. Output per trade: trade id, symbol, side, original stop, breach time/premium, second-reading time/premium, hypothetical exit,
   actual exit, improved/worsened, points after 2 pt/round-trip cost.
Cost of Phase 1: zero live risk. Limit: minute resolution; breach is the actual exit tick.

### Phase 2 - in-worker logging (only if Phase 1 data is not enough; separate approval)
Setting `EnableTwoReadStopShadow` (StrategySettings, SENSEX row, default off, read via `StrategySettingsService`).
Where: the exit loop in `Trading.Worker/ExitWorker.cs`, after `exitDecision.DetermineAsync`, for single-leg signals only.
It writes to existing logs (`AppErrors`-style logger or `StrategyExecutionLogs.Reason`, no new table) and never alters the exit result.
Cost: touches the live exit loop; a bug there is a trading-safety risk, so it needs tests next to the existing exit tests.

## Evaluation (after 15 sessions, not 10)
At about 2.6 SENSEX stops per day, 10 sessions give only about 26 stops; the replay needed 16 days to reach a borderline interval.
Pass/fail fixed in advance (shadow vs actual, SENSEX): net points after costs higher in both halves, PF no lower,
expectancy per trade no lower, max drawdown no worse, CALL and PUT both not worse, and report stops delayed and losses increased
(cancelled exits that later hit a deeper stop). Fail if two-read loses more on confirmed stops than it saves on cancelled ones.

## Risk
- Confirmed stops exit later and at a worse price on real moves (gap-through); it is in the replay but minute data understates tick spikes.
- A resting broker SL_M cannot do this. Live use would mean removing or relaxing the resting stop = weakening protected-stop safety.
  Not proposed. Any live version needs its own proposal and approval.
- Small sample, 16 SENSEX days, one dominant bad day (10-22).

## Approval boundary
Approving this authorises Phase 1 (read-only script and report) now. Phase 2 and any live change need separate explicit approval.
Nothing here deploys, changes a broker order, or edits StrategySettings.
