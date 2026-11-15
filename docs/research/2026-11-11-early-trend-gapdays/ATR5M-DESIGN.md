# 5m ATR + soft EarlyTrendGate - design for approval (2026-11-11)

STATUS: DESIGN ONLY. No source file has been changed for this. Needs "Approved" before any edit.
Research basis: `README` results in this folder (variant E vs A). Classification: PROMISING, not validated.
Goal: reproduce the tested condition exactly, disabled by default; do NOT make the strategy more permissive.

## The condition being reproduced (backtest, `EntryEvaluator.EarlyTrendReason`)
The live gate would block a CALL (5m EMA20 > EMA50) and ALL of the following hold:
1. CALL only (the gate side is already CALL-only).
2. Session opened GapUp or GapDown: gap >= 0.30% either way (`ClassifyOpening`).
3. IST time of day >= 10:30.
4. ATR5m > 0 and (EMA20_5m - EMA50_5m) / ATR5m < 0.30.
Then the gate lets the CALL through. The gap-up morning exception (before 10:00) is untouched and is evaluated first.
The values 0.30, 0.30% and 10:30 are fixed constants, like the existing exception, so no setting can turn this into an untested strategy.

## What already exists (verified in code)
- Live ATR: `AtrService.Calculate(candles, 14)` = Wilder, needs 15 candles. Backtest `IndicatorMath.Atr(bars, 14)` = the same algorithm
  (SMA of first 14 TR, then Wilder smoothing). Identical maths; parity is a matter of identical INPUT bars.
- Live 5m bars: `MarketSnapshot.BuildAsync` already loads `indicatorCandles5m` = last 100 CLOSED 5m bars (`IsClosedNow`, UTC clock). Backtest
  uses a window of 100 closed 5m bars (`Window5m = 100`). Same window, same closed-bar rule.
- `Ema20_5m/Ema50_5m` already live; `DayOpen`, `YesterdayClose`, `GapPct()` already used by the existing exception.
- No `Atr5m` field and no 5m ATR anywhere in live code.

## File-by-file plan
1. `Trading.Worker/SignalGates/MarketSnapshot.cs`
   - Add a property `decimal Atr5m` with default 0 (init-only property, NOT a new positional parameter, so no existing constructor call changes).
   - In `BuildAsync`: `Atr5m = ctx.Atr.Calculate(indicatorCandles5m, 14)` - 0 when fewer than 15 closed 5m bars.
   - Stale guard: Atr5m = 0 if the newest closed 5m bar is older than 10 minutes (two bar periods), so a stalled candle feed can never produce a stale ratio.
     (0 means "missing" and the gate behaves exactly as today.)
2. `Trading.Worker/SignalGates/EarlyTrendGate.cs`
   - New fixed constants: `SoftGapMaxAtr = 0.30m`, `SoftGapMinGapPct = 0.30m`, `SoftGapFrom = 10:30`.
   - New setting key `EnableEarlyTrendSoftGate` (per symbol, default OFF, 0/1).
   - New pure function `SoftGateExempt(signalType, ema20_5m, ema50_5m, atr5m, gapPct, istTimeOfDay, switchOn)` returning a decision record
     (exempt, normalized gap) - every input an argument, so it is unit-testable like `GapUpMorningExempt`.
   - In `BlockAsync`: only when `reason != null` AND the gap-up morning exception did not already exempt it, evaluate the soft rule.
     Missing/zero ATR, missing gap, missing EMA => NOT exempt => the existing block message is returned unchanged.
   - Diagnostic logging (Information) on every evaluation where the gate would block and the switch is on, one line:
     `EarlyTrendGate SOFT_GATE_EVAL {Symbol} {Signal} gap%, ema20_5m, ema50_5m, emaGap, atr5m, normalizedGap, threshold, istTime, decision(EXEMPT|BLOCK), reason`.
3. `Trading.Backtest` - no change needed (the E arm already encodes the condition). Only if parity testing finds a difference.
4. `Trading.Tests`
   - `EarlyTrendSoftGateTests`: boundary tests (0.299 vs 0.30, 10:29:59 vs 10:30, gap 0.29 vs 0.30, ATR 0 / stale, PUT, switch off, exception precedence).
   - ATR parity test: the same synthetic 5m series through `AtrService` and `IndicatorMath.Atr` must agree to a tolerance (guards against drift).
5. `docs/set-early-trend-soft-gate.sql` - enable/rollback/verify script for TradingDB_Paper ONLY (user runs). Not executed by Claude.

Nothing else changes: no exits, no order flow, no new entry rules, no VWAP/breakout, no change to the gap-up exception, no schema change
(IndicatorSnapshot has no ATR5m column; diagnostics live in the log, a column is an optional later migration).

## Safety / risk
- Disabled by default; no row = off. Flag per symbol; kill switch = set the row to 0 (5-minute settings cache).
- Fail-safe: every missing/stale input falls back to the existing block. The change can only ALLOW a CALL the old gate blocked, and only
  when every tested condition holds. Downstream gates (duplicate, risk, stop floor, one-position, OI, R:R, spread) still run; exits and
  duplicate-SELL protection are untouched.
- Parity risks to verify, not assume: (a) live `GapPct` uses `DayOpen/YesterdayClose` (feed values) while the backtest uses the first 1m bar open vs
  prior close - compare on past sessions; (b) 5m candle history was rebuilt on 2026-09-30, earlier bars are not comparable; (c) cadence: live
  evaluates at signal-build time, the backtest per closed minute.
- Performance: one ATR over <= 100 bars per snapshot build; negligible.

## Verification before any trial
1. Build + `dotnet test src/Trading.Tests` (report the pre-existing `MarketRegimeTradeFilterTests` failure separately).
2. Parity check on history: recompute ATR5m through the live code path for a sample of past minutes and compare with the backtest series; max abs difference must be ~0.
3. Docker/health checks are the user's.

## Forward paper trial (after deploy, PAPER database only)
- Enable on TradingDB_Paper for BANKNIFTY and SENSEX (where the added trades came from; NIFTY added none), leave live DB untouched.
- Collect `SOFT_GATE_EVAL` lines; qualifying gap sessions ~10. Success = added trades net positive after costs, no drawdown increase, and the
  normalized gap in the log matches a backtest recompute for the same minute. Stop/rollback if the first 5 added trades lose > the average research loss.
- Decision to go live is a separate approval.

## Not in scope
VWAP, 15-minute-high breakout/continuation, changes to the early-trend side/threshold semantics, Normal-day softening (rejected).

## Implementation notes (2026-11-11)
- Stale rule is explicit: `MarketSnapshotBuilder.Atr5mMaxCloseAge = 10 min`, measured from the newest closed 5m bar's CLOSE (CandleTime + 5 min). That is 15 min after
  the bar's start time. (The first draft said "older than 10 minutes" without saying from start or close.)
- `EarlyTrendGate.BlockAt(ctx, market, signal, nowIst, ct)` takes the IST clock as an argument; `BlockAsync` passes `IstClock.Now`. Behaviour unchanged.
- Gap-day parity (read-only): `gap_parity.py` (72 sessions since 10-07, snapshot DayOpen/YesterdayClose vs backtest) and `gap_parity_ticks.py`
  (what the gate really reads: MarketTick.Open vs first 1m candle open, 12 symbol-sessions, ticks kept ~7 days): 0 classification disagreements,
  tick open == candle open on every compared session. Residual risk: the gate uses the tick open, so a feed fallback glitch (see memory
  index-tick-spikes) could mis-classify a session; not seen in the sample.
- Known unrelated failing test: `MarketRegimeTradeFilterTests.UnsafeVolatility_IsRejected_EvenWhenTrending(Extreme)`. The working tree's uncommitted
  MarketRegimeService.cs change removed the Extreme-volatility rejection; the committed test still expects it.
