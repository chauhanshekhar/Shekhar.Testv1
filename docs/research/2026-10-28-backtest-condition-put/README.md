# /backtest-condition PUT (2026-10-28)

**Verdict: No historically validated edge found under the tested assumptions.** The live band (RSI5m 28-38) is
kept as is; nothing to change.

## Data
- Source: `docs/research/2026-10-26-backtest-condition-both/snapshots-outcomes.csv`. Each snapshot row and its outcome
  are on the same row. Outcomes come from Candles, with **no IndicatorSnapshotOutcomes join**.
  The skill's helper scripts (`snapshot_outcomes.py` and the others) were deleted from
  `.claude/skills/backtest-condition/` while this run was in progress, so the export was **not regenerated**.
- Gap: Candles daily first 09:15 open vs previous session close (read-only query, saved as `daily-open-close.csv`).
  Gap-Up >= +0.30%, Gap-Down <= -0.30%. If the previous session is missing or more than 4 days earlier, the gap is Missing.
- Coverage: 2026-07-29 .. 2026-10-26. There are 42 PUT days and 14,123 PUT minutes (`TrendDirection` = Bearish).
  - Symbols: SENSEX 5,750, BANKNIFTY 4,313, NIFTY 4,060.
  - 0 duplicate (Symbol, minute) rows.
  - Missing values: Rsi5m 13% (pre-Sep rows only), Vwap 55% (excluded as a feature).
- Live config verified (read-only): `PutRsiFloor` 28, `PutRsiCeiling` 38, global, active.

## Outcome model (the 10-26 export's model; costed)
- Entry at snapshot Ltp. Stop = clamp(1.25 x ATR points, 20, 100). Target = 1.5 x ATR points.
- Scan horizon is 45 min. If one candle touches both the stop and the target, it counts as a stop.
- P&L = index move x delta 0.5 x lot, minus Rs20/order and 1 pt slippage per side.
- Within each cell, a new entry starts only after the previous trade on that symbol has exited (non-overlapping), so
  overlapping minutes are not counted twice.
- This model is a base rate, not the live ladder/trail exit. Trading.Backtest with the live exits gave PUT PF 1.49 on 10-26.
- Day split, chronological:
  - train: before 09-30, 25 days
  - val: 09-30..10-12, 8 days
  - hold: 10-13..10-26, 9 days
- Timeouts are reported separately (6 total).

## Baselines (all days, non-overlapping)
| | N | Days | Target-first % | Net Rs | PF | MaxDD | Train Net / PF | Val Net / PF | Hold Net / PF |
|---|---|---|---|---|---|---|---|---|---|
| No filter | 3,579 | 42 | 50.6 | -287,335 | 0.71 | 289,602 | -132,594 / 0.51 | -41,186 / 0.87 | -113,555 / 0.72 |
| Live RSI5m 28-38 | 1,097 | 28 | 55.8 | -50,892 | 0.83 | 57,714 | -34,799 / 0.45 | -9,858 / 0.92 | -6,235 / 0.95 |

## RSI5m bands (all days)
| Band | N | Days | Tgt% | Net | PF | Hold Net / PF |
|---|---|---|---|---|---|---|
| <28 | 380 | 20 | 52.6 | -36,886 | 0.70 | -16,939 / 0.68 |
| **28-38 (live)** | 1,097 | 28 | 55.8 | -50,892 | **0.83** | -6,235 / 0.95 |
| 38-45 | 1,082 | 30 | 48.5 | -133,176 | 0.61 | -97,064 / 0.45 |
| 45-55 | 1,011 | 31 | 48.2 | -103,604 | 0.65 | -59,109 / 0.65 |
| 55-65 | 268 | 24 | 47.8 | -25,741 | 0.61 | -6,956 / 0.66 |
| >=65 | 26 | 13 | 65.4 | +237 | 1.05 | n=3, insufficient |
| Missing | 443 | 12 | 43.8 | -47,741 | 0.56 | — (diagnostic only) |

- The live 28-38 band has the highest PF of all the bands, but no band is profitable.
- The finer sweep (floors 0-35 x ceilings 32-101) found no window with positive train Net. The best totals were
  [30,38) and [32,38): hold PF 1.05-1.07, but the train PF was 0.43-0.46.

## Gap, ATR, EMA, combinations
- Gap regimes:
  - All are negative overall: Gap-Down PF 0.70 (6 days), Gap-Up 0.92 (11 days), Normal 0.66 (33 days).
  - Gap-Up was positive only in the holdout, and those were just 2 days.
  - Per-gap RSI bands are **insufficient**: train has 3 Gap-Down days and 4 Gap-Up days.
- ATR: all four bands have PF < 0.75.
- EMA:
  - Bearish-aligned (aligned with the trade) PF 0.73; Mixed 0.68.
  - Bullish/opposed never occurs, because PUT rows are Bearish-trend minutes. Missing never occurs.
- Combinations: 279 cells tested (single, 2-way, 4-way with n>=30, and RSI windows).
  - 19 cells had train Net > 0 and PF > 1.1. All of them had fewer than 20 train trades or only 1-3 train days.
  - **0 cells passed** the screens: train n >= 100, train days >= 20, val Net > 0, hold n >= 30, hold Net > 0, hold PF > 1.1.

## Rejected (why)
- Gap=GapUp & ATR High: hold +17,323 PF 1.33, but train had 3 trades on 1 day and val lost -6,242. This is a one-slice result.
- 4-way GapUp & 45-55 & High & Mixed: the only cell with val and hold both positive. It covers 4 days total and had 1 trade in train.
- RSI5m >= 65: n = 26.
- Hour = 9: n = 54 on 8 days, with one day > 5x the total net.

## Caveats
- Rsi5m before 2026-09-30 13:30 came from rebuilt `_5m` history (see memory: "5m" candles were 1m bars).
  The whole training window falls in that period.
- Only 9 holdout days. Results are exploratory.

## Next action
- None. Re-run after about 15 more sessions, once the skill's export scripts are restored. Rerun with:
  `python docs/research/2026-10-28-backtest-condition-put/sweep_put.py`
- Files: `sweep_put.py`, `cells.csv` (all 279 cells: train/val/hold/all metrics), `daily-open-close.csv`.
