# /backtest-condition CALL: combination grid (2026-10-28), simulated only

**Verdict: No historically validated edge found under the tested assumptions.** 0 of 878 CALL cells pass.

## Data
- **Source:** IndicatorSnapshots + IndicatorSnapshotOutcomes from 2026-09-30.
  - Alignment audit: 908,246 rows, 0 orphans, 100% matched on symbol and price.
- **Rows:** 2,906 CALL symbol-minutes over 19 days, 120 with neither barrier hit.
  - Every CALL outcome row is Trend = Bullish with Ema20 > Ema50, so `Ema1m` and `Trend` are constant for CALL.
  - Delta is Missing on 90% of rows.
- **Split:**
  - train: 09-30..10-15
  - val: 10-16..10-22
  - hold: 10-23..10-28 (only **4** days with CALL rows)
- **Outcome:** stored ±20-index-point first-hit result, 15-min window. Rs per lot = pts × 0.5 × lot − Rs40 − 2 pts × lot.

## Baseline and single features
| Cell | N | Days | Tgt% | Net | PF | Train / Val / Hold Net |
|---|---|---|---|---|---|---|
| No filter | 2,906 | 19 | 49.8 | -292,470 | 0.48 | -138,970 / -81,460 / -72,040 |
| Rsi5m 45-55 | 900 | 19 | 49.1 | -98,590 | 0.46 | -39,780 / -41,000 / -17,810 |
| Rsi5m 55-65 | 1,256 | 19 | 48.7 | -135,520 | 0.46 | -61,430 / -34,300 / -39,790 |
| Rsi5m >=65 | 874 | 15 | 52.5 | -72,760 | 0.54 | -43,290 / -9,090 / -20,380 |
| Pcr <0.8 | 347 | 16 | 59.7 | -16,540 | 0.70 | **+3,850** / -15,780 / -4,610 |
| Ema5m 20<=50 | 1,003 | 18 | 50.6 | -92,950 | 0.52 | -34,530 / -40,470 / -17,950 |
| Atr <0.03 | 354 | 17 | 53.7 | -25,220 | 0.55 | -13,090 / -11,500 / -630 |

- No single feature is profitable in all three periods.
- PCR < 0.8 has the best target-first rate (60%), but it lost in validation and holdout.

## Combinations
- 9 cells were positive in both train and validation. None passes:
  - Each one either lost in the holdout, or had **0** holdout trades. The Gap-Up cells have no Gap-Up holdout days.
  - Each has 13-98 train trades.
- The best of them, Rsi5m >= 65 & Ema5m 20<=50 (Delta Missing): train +1,860, val +1,080, hold -1,480 (26 trades), total PF 1.10.
  It is exploratory only.

## Why everything is negative
- The barrier is symmetric (±20 index points = ±10 option points at delta 0.5). About 50% target-first is therefore break-even before costs.
- Costs (Rs40 + 2 pts × lot) turn that into roughly -Rs100 per trade. A cell needs about 60%+ target-first just to break even.
- Treat this grid as a ranking tool between cells, not as income.

## Limitations
- Only 19 days; holdout of 4 days. Many cells were tested, so false positives are likely.
- The fixed 20-point barrier is not the live ATR stop, ladder or trail. There is no option premium path.

## Next action
- None. CALL settings unchanged.
- Re-run with `--window 10` or after about 10 more sessions:
  `python .claude/skills/backtest-condition/combo_grid.py docs/research/<date>-backtest-condition-call-grid --dir CALL`
