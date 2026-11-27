# Proposal: let the backtest check exits against the per-second option path

Status: PROPOSAL ONLY. No code written. Needs explicit approval ("Proceed" / "Implement") before anything under `src/` changes.

## Problem
Replay exits differ from live exits. Signal 10449 (SENSEX CALL 2026-11-25 10:56): live `PROTECTED_SL_HIT` at 10:57:58, P&L 0.00;
replay `TRAILING_SL_HIT`, +4,386. The replay cannot see inside a minute.

## Root cause
`Trading.Backtest/Simulation/ExitReplay.cs` walks a premium path of 1-minute closes (`path` = move at each following minute close).
Premium comes from `OptionMinuteHistory` (one row per minute). A stop touched inside a minute and reversed by the close is invisible.
`ExitReplay.RunFixedBracket` documents the same limit. Live exits run once per second on real quotes.

## What exists
`SignalOptionTicks` (uncommitted migration `20261125073727_AddSignalOptionTicks`, script `docs/add-signal-option-ticks.sql`):
per open signal per ExitWorker cycle: OptionLtp, Source (QUOTE / CHAIN / NONE), OptionSL, ProtectedSL, Status. Kept 90 days.
Not deployed, not read by the backtest. Rows exist ONLY for signals live actually held, and only while open.

## Limits that shape the design
1. No rows for replay-only entries, so a per-second path cannot change simulated results for hypothetical trades.
2. `ExitReplay.Run` steps one minute at a time (e.g. "T1 sale first, mid sale next minute"). Running the full ladder per second
   would mean changing those step rules - a large, risky change. Not proposed.
3. `Source = CHAIN` rows can be up to ~30 s stale; they must be reported separately from QUOTE rows.
4. History starts the first session after deploy. Nothing exists for signals before that.

## Proposed change (Phase 1, diagnostic only - simulation results unchanged)
New read-only mode `--tick-check [--from yyyy-MM-dd]` in `Trading.Backtest`:
For every live signal that has `SignalOptionTicks` rows, print one line:
signal id, side/symbol, live exit reason + time, replay exit reason + time (existing minute-path replay),
first tick where OptionLtp <= OptionSL or ProtectedSL (stop touch time, QUOTE rows only), count of QUOTE vs CHAIN ticks,
and a verdict: MINUTE_PATH_MISSED_STOP / SAME / TICKS_INSUFFICIENT.
Output: `docs/research/<date>-tick-check/tick-check.csv` + summary.

### Affected files
- `src/Trading.Backtest/Program.cs` - register the `--tick-check` mode (follows the existing `--signal-parity` pattern).
- `src/Trading.Backtest/Data/HistoricalDataLoader.cs` - one read-only query: `SignalOptionTicks` filtered by `TickTime >= @from`
  (date-filtered, so it does not add to the full-table load that already slows runs).
- NEW `src/Trading.Backtest/Simulation/TickPathCheck.cs` - pure comparison logic (no I/O) so it is unit-testable.
- NEW `src/Trading.Tests/Backtest/TickPathCheckTests.cs` - cases: stop touched intra-minute, no touch, only CHAIN rows, no rows.
No change to `ExitReplay`, `ExecutionSimulator`, any gate, StrategySettings, or the live Worker.

## Phase 2 (only after Phase 1 shows how often the minute path misses stops - separate approval)
Option: a flag `--exit-path ticks` that, for matched live signals with enough QUOTE ticks, truncates the replay exit at the first
stop touch. Replay-only entries keep the minute path. Decide from Phase 1 data, not now.

## Risk
Low for Phase 1: read-only, offline, new code only; live trading, orders and settings untouched. Risk is misreading results:
the check covers only held signals, so it measures replay error on live trades and says nothing about replay-only entries.

## Validation
`dotnet build TradingPlatform.slnx` and `dotnet test src/Trading.Tests` pass (report pre-existing failures separately);
run `--tick-check` after the table exists and one session has recorded ticks; confirm signal 10449-like cases are flagged.

## Deployment impact
None for Phase 1 (backtest tool only). Prerequisite for it to produce output: you run `docs/add-signal-option-ticks.sql` (or the EF
migration) and deploy the Worker with `SignalOptionTickRecorder`. Claude does neither.
