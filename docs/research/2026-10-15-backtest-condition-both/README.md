# Backtest condition sweep — CALL and PUT (2026-10-15)

Read-only. Nothing was written to the database and no setting was changed.

## How this was produced

1. **Replay** (`src/Trading.Backtest`, 694 configurations per side × 4 scopes, 68 sessions from
   2026-07-10) — `grid.csv`, `trades.csv`, `ranking.md`, `run.log`:

   ```bash
   dotnet run --project src/Trading.Backtest -- --side BOTH --dump-trades --out docs/research/2026-10-15-backtest-condition-both
   ```

   The replay in this folder was produced earlier today and reused on the second run of the
   command: the three exports below returned identical row counts (20,682 / 162 / 22), so the
   inputs had not moved and a second 20-minute replay would have reproduced the same file.

2. **Read-only exports** — `.claude/skills/backtest-condition/export-csv` over `snapshots.sql`,
   `real-signals.sql`, `settings.sql` → `snapshots.csv` (20,682 symbol-minutes),
   `real-signals.csv` (162 closed signals), `settings.csv`.

3. **Tables** — `analyze_grid.py` → `grid-tables.txt`, `analyze_features.py`
   (`CALL|NoRsi+NoEmaGap`, `PUT|NoRsi+NoEmaGap`) → `feature-tables.txt`, `analyze_real.py` →
   `real-tables.txt`.

4. **Explorer** — `make_explorer.py` → `explorer.html`, served from `docs/research`
   (`.claude/launch.json` → `research-explorer`):
   <http://localhost:8899/2026-10-15-backtest-condition-both/explorer.html>

The free-form `--cond "NAME:expr"` grammar in `SKILL.md` has no implementation; the feature
definitions were applied by bucketing `analyze_features.py` / `analyze_real.py` output instead.
`IndicatorSnapshotOutcomes` was never joined.

## Settings used (resolved live, applied to every day)

CALL RSI 45–65, PUT RSI 28–38 (5-minute RSI), `CallPcrMinimum` 0.8 global, `PutPcrMinimum` 0.7
SENSEX only, `MinAtrPercent` 0.03, `EnableEarlyTrendGate` 1, `EnableNoTradeWindowGate` 1,
`EnableBreakevenArm` 1, `EnableBreakevenPartialSale` 0, ladder 3R/5R, `AtrStopLossMultiplier` 1.25,
stop clamp 20–100 index points, `AdaptiveLotMin` 4. Full list in `settings.csv`.

## Assumptions — read before using any number

- Stop model `Live` (breakeven arm, stop to entry after a partial, ATR trail), bar-close
  granularity, fills at the stop level.
- Premium path: real `OptionMinuteHistory` where it exists (**only 5 of 35 CALL and 26 of 146 PUT
  baseline trades**), otherwise index move × that symbol's average `DeltaAtEntry`.
- **Costs and slippage are not modelled** (`NetProfitAfterCosts` is NULL).
- `Pcr`/`MarketState` come from `IndicatorSnapshots`, which cover only **32 % of decision minutes**
  (37 session days, 2026-07-29 → 2026-10-15), so every PCR conclusion is weak.
- `BlockedDelta` is present on 1,007 of 20,682 minutes (4.9 %) — simulated Delta cells are
  **NOT TESTABLE**; real trades use `DeltaAtEntry` and are fine.
- VWAP is populated on 12 snapshot days, of which only 2026-10-09 and 10-12…10-15 are live
  futures-VWAP sessions — **NOT TESTABLE**.
- Not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread,
  StaleEntry — the replay sees ideal data.
- Scope `ALL` = all symbols sharing **one** open position; a symbol scope = one open position in
  that symbol. The two are not sums of each other.
- Real-trade P&L is as journaled: paper and live mixed, costs not re-applied, and **2026-09-30 was
  restated** (4 lots, unfilled exits booked) — it alone is +12,128 of the PUT total.
- `Validated` in `grid.csv` ignores sample size; the explorer re-labels it **THIN** unless both
  halves hold ≥ 15 trades. 280 rows are YES, 275 THIN — **every YES row is PUT**.

## Result in one line

PUT makes money and the evidence says widen its RSI band for BANKNIFTY (25–42) and drop the SENSEX
PUT PCR floor; CALL is break-even in the real journal (+₹822 / 60 trades) and **not one CALL
configuration out of 694 clears the validation bar**, so nothing should change for CALL.

## Candidates (validated = positive net and PF > 1 in both halves, ≥ 15 trades in each)

| # | change | key | scope | Trades | Days | Net ₹ | PF | Val n / net | Test n / net |
|---|---|---|---|--:|--:|--:|--:|---|---|
| — | baseline | — | BANKNIFTY | 109 | 39 | +34,462 | 1.34 | 31 / −2,691 | 27 / +36,400 |
| 1 | RSI band 28–38 → **25–42** | `PutRsiFloor`, `PutRsiCeiling` | BANKNIFTY | 135 | 44 | **+65,807** | 1.56 | 35 / +13,617 | 35 / +39,228 |
| 2 | ceiling only 38 → **42** | `PutRsiCeiling` | BANKNIFTY | 126 | 41 | +56,468 | 1.52 | 33 / +6,257 | 34 / +35,297 |
| — | baseline | — | SENSEX | 58 | 15 | +28,739 | 1.86 | 2 / +3,452 | 49 / +17,407 |
| 3 | deactivate the PUT PCR floor | `PutPcrMinimum` (SENSEX) | SENSEX | 132 | 40 | +35,854 | 1.47 | 32 / +5,515 | 53 / +21,252 |

Not proposed, but the direction of travel: `PUT|NoRsi` on BANKNIFTY (no band at all) is also
validated — 165 trades, 47 days, +81,272, PF 1.58. The band is costing money on that symbol
rather than protecting it.

None of these is applied. A settings change ships as an idempotent `docs/` script with ROLLBACK
and VERIFY blocks, per `.claude/rules/02-approval-gate.md`.

## Files

| file | what it is |
|---|---|
| `explorer.html` | browsable view — Config grid + Daily trades + one tab per CSV |
| `grid.csv` | 5,576 rows: every configuration × scope, full metrics + train/validation/test |
| `ranking.md` | the engine's own ranking (training + validation only) |
| `trades.csv` | every simulated trade (33 MB) — the source of all per-day columns |
| `grid-tables.txt` | configuration ranking with Days / T-day / W-day / L-day / Net-day |
| `feature-tables.txt` | SIMULATED feature grid (RSI 1m & 5m, PCR, delta, day change, ATR, EMA20, VWAP, body) |
| `real-tables.txt` | ACTUAL CLOSED TRADES, same feature grid |
| `real-signals.csv`, `snapshots.csv`, `settings.csv` | the read-only exports |
