# /backtest-condition CALL (2026-10-28) — historical simulation only

**Verdict: No historically validated edge found under the tested assumptions.** 0 of 261 CALL cells passed.
No live-trade data was used anywhere in this run.

## Data and method
This run uses the same method as `../2026-10-28-backtest-condition-put/`; only the direction differs.
- **Source:** `docs/research/2026-10-26-backtest-condition-both/snapshots-outcomes.csv`.
  - Each snapshot and its outcome are on the same row. Outcomes are simulated from candles, with no IndicatorSnapshotOutcomes join.
  - CALL = the snapshot's `TrendDirection` is Bullish: 2026-07-29..10-26, 43 days.
  - The skill's export scripts are gone, so this data was not refreshed past 10-26.
- **Simulated outcome:**
  - Entry at the snapshot Ltp. Stop = clamp(1.25 × ATR points, 20, 100). Target = 1.5 × ATR points. 45 min horizon.
  - If one candle touches both the stop and the target, it counts as a stop.
  - P&L = index move × delta 0.5 × lot, minus Rs20/order and 1 pt slippage per side.
  - Within each cell, one position per symbol at a time (no overlapping trades).
- **Features:**
  - Gap from the Candles 09:15 open vs the previous close (±0.30%).
  - RSI5m bands, plus a finer sweep: floors 0-65 × ceilings 45-101.
  - ATR% bands, EMA alignment (bullish = with the trade, bearish = against), symbol, hour.
  - All 2-way combinations, and 4-way combinations with n ≥ 30.
- **Chronological split by day:**
  - train: before 09-29, 25 days
  - validation: 09-29..10-12
  - holdout: 10-13..10-26, 9 days
- **Pass rule:** train n ≥ 100 and ≥ 20 days, val Net > 0, holdout n ≥ 30, holdout Net > 0, holdout PF > 1.1.
- **Live settings, read-only:** `CallRsiFloor` 45 / `CallRsiCeiling` 70, and `GapUpCallRsiFloor` 45 / `GapUpCallRsiCeiling` 85.
  Both are used only as a baseline.

## Baselines (simulated)
| | N | Days | Target-first % | Net | PF | MaxDD | Train Net / PF | Val Net / PF | Hold Net / PF |
|---|---|---|---|---|---|---|---|---|---|
| No filter | 2,880 | 43 | 45.9 | -346,895 | 0.59 | 349,713 | -127,277 / 0.43 | -107,127 / 0.64 | -112,491 / 0.66 |
| Live 45-70 (Gap-Up 45-85) | 2,100 | 31 | 47.5 | -235,298 | 0.62 | 241,822 | -43,886 / 0.43 | -88,126 / 0.66 | -103,286 / 0.64 |

## RSI5m bands (simulated, all days)
| Band | N | Days | Tgt% | Net | PF | Hold Net / PF |
|---|---|---|---|---|---|---|
| <28 | 6 | 2 | 0 | -3,940 | 0.00 | insufficient |
| 28-38 | 43 | 16 | 41.9 | -8,774 | 0.32 | insufficient |
| 38-45 | 220 | 25 | 45.9 | -27,141 | 0.55 | -7,188 / 0.50 |
| 45-55 | 894 | 30 | 45.2 | -104,039 | 0.63 | -42,842 / 0.67 |
| 55-65 | 1,196 | 30 | 48.3 | -129,498 | 0.63 | -74,887 / 0.57 |
| >=65 | 685 | 25 | 50.4 | -69,384 | 0.66 | -20,523 / 0.77 |
| Missing | 409 | 13 | 37.4 | -65,138 | 0.46 | diagnostic only |

- No RSI5m window was profitable in training. The best total PF was 0.69, for [55,60).
- The top of the gap-up band (70-85) adds no edge: [65,85) has PF 0.67.

## Gap, ATR, EMA
- **Gap regimes:** all have PF < 0.6 (Gap-Up 0.59, Gap-Down 0.54, Normal 0.59).
  - The training period has only 3-4 gap days of each kind, so per-gap RSI bands are **insufficient**.
- **ATR:** every band has PF < 0.8.
- **EMA:** bullish-aligned (with the trade) PF 0.67 vs mixed 0.57.
  - In the holdout the aligned group is the least bad (PF 0.92), but it lost in train and validation. It is not a candidate.
- **Combinations:** 16 cells were profitable in training, all with 1-3 train trades.
  - Two were also positive in validation:
    - Normal & 55-65 & High ATR & Aligned: holdout -9,310.
    - 55-65 & High ATR: holdout -52,461.
  - Both are rejected.
- **Hour = 9:** holdout +9,173 PF 2.84 on 18 trades over 3 days, but it lost in train and validation. Rejected, too small.

## Limitations
- Under this simple exit model, simulated CALL loses in every slice.
  - It is a base-rate comparison between filters, not the live ladder/trail exit.
  - It does not apply EarlyTrendGate or the PCR filter.
- Training-period RSI5m comes from the rebuilt `_5m` history. Only 9 holdout days.

## Next action
- None. Keep the CALL settings unchanged; no live configuration was touched.
- Re-run once the export scripts are restored and about 15 more sessions exist:
  `python docs/research/2026-10-28-backtest-condition-call/sweep_call.py`
- Files: `sweep_call.py`, `cells.csv`, `daily-open-close.csv`.
