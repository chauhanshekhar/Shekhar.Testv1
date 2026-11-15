# /backtest-condition BOTH — 2026-11-03 (1- to 4-condition, merged)

**Conclusion: 3. No historically validated edge found.** 0 of 5,296 cells pass (CALL 0/2,388, PUT 0/2,908).

## Method
- `combo_grid.py --dir BOTH --window 15 --max-k 3` -> `grid.csv` (1,973 cells); `--max-k 4` -> `k4/grid.csv` (5,296 cells). The k<=3 rows in k4 match grid.csv exactly (1,973/1,973, 0 all_Net mismatches). `candidate-results.csv` and the training/validation/holdout CSVs are built from k4/grid.csv.
- Fixed index-point target/stop barriers over a 15-minute window from IndicatorSnapshotOutcomes, with costs, Rs/lot. **Not** an option-premium replay. No live trade P&L used.
- 23 days, 2026-09-30..2026-11-03. Train 09-30..10-20 (13 d), validation 10-21..10-27 (5 d), holdout 10-28..11-03 (5 d).
- Observations: 1,087,364 aligned rows (0 orphans) -> 23,813 after one per symbol-minute-direction.
- Pass rule: train net > 0; validation net > 0; holdout N >= 30, net > 0, PF > 1.1.
- **PF = inf** means the cell had no simulated losing trades in that split (gross loss 0). It appears only on holdouts of 1-9 trades and is not evidence of an edge.

## Cell counts (reconciled with k4/grid.csv)
| Dir | Base | k=1 | k=2 | k=3 | k=4 | Total | Delta=Missing duplicates | Pass | Hold net > 0 * | Hold N >= 30 * | All net > 0 * |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| CALL | 1 | 22 | 180 | 704 | 1,481 | 2,388 | 669 | 0 | 348 | 945 | 57 |
| PUT | 1 | 23 | 202 | 840 | 1,842 | 2,908 | 757 | 0 | 225 | 660 | 17 |

\* Excluding Delta=Missing duplicates (Delta is 87% missing, so those cells copy the cell without Delta).

Only cell family positive in train, validation AND holdout: CALL `Ema5m bear & GapUp` (plus 3 identical variants adding Ema1m bull / Trend=Bullish): train 98 tr / +1,600, validation 17 tr / +280, holdout 25 tr on 1 day / +600, PF 1.25. Fails holdout N >= 30.

## Baseline (live-condition definitions, simulated)
| Cell | Train N / Net | Val N / Net | Hold N (days) / Net / PF | All Net / PF |
|---|---|---|---|---|
| CALL no filter | 1,898 / -181,040 | 812 / -97,920 | 941 (5) / -72,170 / 0.55 | -351,130 / 0.49 |
| CALL Rsi5m 55-65 | 752 / -74,560 | 397 / -49,780 | 391 (5) / -34,630 / 0.51 | -158,970 / 0.47 |
| CALL Rsi5m 45-55 | 533 / -66,810 | 306 / -31,610 | 139 (5) / -3,060 / 0.84 | -101,480 / 0.48 |
| PUT no filter | 1,968 / -169,370 | 1,146 / -101,220 | 583 (5) / -60,490 / 0.46 | -331,080 / 0.52 |
| PUT Rsi5m 28-38 (live) | 686 / -50,060 | 397 / -26,510 | 142 (4) / -15,160 / 0.47 | -91,730 / 0.58 |
| PUT Rsi5m 45-55 | 528 / -44,790 | 359 / -35,750 | 213 (5) / -21,320 / 0.48 | -101,860 / 0.52 |

## Top CALL by holdout net (all FAIL)
| Conditions | Train N / Net | Val N / Net | Hold N (days) | Hold Net | PF | All Net | Fails on |
|---|---|---|---|---:|---:|---:|---|
| Pcr<0.8 & Gap=Normal | 169 / -8,800 | 74 / -5,680 | 26 (2) | 3,500 | 3.11 | -10,980 | train, val, N |
| Pcr<0.8 & Ema5m bull & Gap=Normal | 130 / -3,830 | 41 / -3,020 | 26 (2) | 3,500 | 3.11 | -3,350 | train, val, N |
| Pcr 0.8-1.0 & Atr 0.04-0.05 & Ema5m bear | 85 / -16,110 | 19 / -960 | 11 (2) | 3,420 | 11.06 | -13,650 | train, val, N |
| Pcr<0.8 & Atr<0.03 | 52 / -5,030 | 15 / +840 | 24 (3) | 2,760 | 4.29 | -1,430 | train, N |
| Rsi 55-65 & Pcr<0.8 & Gap=Normal | 70 / -6,740 | 44 / -5,040 | 21 (2) | 2,760 | 2.44 | -9,020 | train, val, N |
| Pcr 0.8-1.0 & Ema5m bear | 243 / -31,090 | 107 / -9,870 | 43 (2) | 2,530 | 1.68 | -38,430 | train, val |
| Pcr 0.8-1.0 & Ema5m bear & GapDown | 60 / -17,250 | 17 / -1,920 | 39 (1) | 2,330 | 1.70 | -16,840 | train, val |
| Rsi>=65 & Pcr 0.8-1.0 & Atr 0.04-0.05 & Normal | 49 / -1,920 | 3 / -1,520 | 14 (2) | 1,970 | 2.99 | -1,470 | train, val, N |
| Rsi 55-65 & Pcr<0.8 & Atr>=0.05 & Normal | 25 / +90 | 22 / -3,020 | 16 (1) | 1,440 | 1.75 | -1,490 | val, N |
| Ema5m bear & GapUp | 98 / +1,600 | 17 / +280 | 25 (1) | 600 | 1.25 | 2,480 | N |

## Top PUT by holdout net (all FAIL)
| Conditions | Train N / Net | Val N / Net | Hold N (days) | Hold Net | PF | All Net | Fails on |
|---|---|---|---|---:|---:|---:|---|
| Rsi 45-55 & Ema5m bear & GapDown | 57 / -1,790 | 42 / -8,450 | 9 (2) | 2,080 | inf | -8,160 | train, val, N |
| Rsi 45-55 & Atr>=0.05 & Ema5m bear | 74 / -5,900 | 145 / -15,170 | 8 (1) | 1,880 | inf | -19,190 | train, val, N |
| Rsi 38-45 & Pcr 0.8-1.0 & Ema5m bear & GapDown | 53 / -2,410 | 1 / +120 | 6 (1) | 1,800 | inf | -490 | train, N |
| Rsi 38-45 & Pcr 0.8-1.0 & Atr>=0.05 & GapDown | 17 / -3,520 | 0 / - | 6 (1) | 1,800 | inf | -1,720 | train, val, N |
| Rsi 38-45 & Pcr 0.8-1.0 & Ema5m bear | 195 / -21,440 | 26 / -5,170 | 8 (2) | 1,640 | 6.86 | -24,970 | train, val, N |
| Rsi 38-45 & Pcr 0.8-1.0 & GapDown | 55 / -2,610 | 1 / +120 | 7 (1) | 1,520 | 6.43 | -970 | train, N |
| Rsi 28-38 & Atr 0.04-0.05 & GapUp | 27 / -2,450 | 60 / -1,460 | 12 (1) | 1,070 | 2.26 | -2,840 | train, val, N |
| Rsi 45-55 & Pcr 1.0-1.2 & GapDown | 27 / -130 | 18 / -2,020 | 11 (2) | 990 | 2.36 | -1,160 | train, val, N |
| Pcr>=1.2 & Atr<0.03 | 89 / -6,450 | 19 / -6,860 | 5 (2) | 920 | inf | -12,390 | train, val, N |
| Rsi 38-45 & Pcr 1.0-1.2 & Ema5m bear & Normal | 128 / -8,670 | 65 / -9,700 | 6 (1) | 900 | 10.00 | -17,470 | train, val, N |

Best PUT with holdout N >= 30: Rsi 45-55 & GapDown, 36 tr (2 d) +540 PF 1.13; validation -12,610, all -13,870.

## Gap x RSI x PCR (3-condition)
| Gap | Dir | Cells | Hold net > 0 | Sum all-net | Best: Hold N (days) / Net / PF; All Net |
|---|---|---:|---:|---:|---|
| GapUp | CALL | 11 | 1 | -118,110 | Rsi 45-55 & Pcr<0.8: 15 (2) / +230 / 1.16; -1,950 |
| GapDown | CALL | 3 | 2 | -31,970 | Rsi 45-55 & Pcr 0.8-1.0: 17 (1) / +720 / 1.43; -9,390 |
| Normal | CALL | 13 | 3 | -255,620 | Rsi 55-65 & Pcr<0.8: 21 (2) / +2,760 / 2.44; -9,020 |
| GapUp | PUT | 12 | 1 | -74,350 | Rsi 28-38 & Pcr>=1.2: 1 (1) / +120 / inf; -12,090 |
| GapDown | PUT | 11 | 4 | -81,640 | Rsi 38-45 & Pcr 0.8-1.0: 7 (1) / +1,520 / 6.43; -970 |
| Normal | PUT | 18 | 1 | -240,520 | Rsi 55-65 & Pcr 0.8-1.0: 3 (2) / +720 / inf; -1,940 |

High RSI + Low PCR: CALL Rsi>=65 & Pcr<0.8 & Normal (train 68 / +4,620, validation -620, holdout 8 tr (2 d) -820); CALL Rsi 55-65 & Pcr<0.8 & GapUp (holdout 20 (3) -710, all -400). PUT: no cell met the grid's minimum count.

Low RSI + High PCR: PUT 10 cells, all negative overall (best Rsi 28-38 & Pcr 1.0-1.2 & GapUp -3,190, holdout 27 (2) -260; worst Rsi 28-38 & Pcr 1.0-1.2 & Normal -25,090, holdout PF 0.17). CALL: no cell met the minimum count.

## Four-condition (Gap + RSI + PCR + X)
| X | Dir | Cells | Hold net > 0 | Best: Hold N (days) / Net / PF; All Net |
|---|---|---:|---:|---|
| EMA1m | CALL | 27 | 6 | Rsi 55-65 & Pcr<0.8 & bull & Normal: 21 (2) / +2,760 / 2.44; -9,020 |
| EMA5m | CALL | 35 | 7 | Rsi 55-65 & Pcr<0.8 & Ema5m bull & Normal: 21 (2) / +2,760 / 2.44; -5,830 |
| ATR | CALL | 62 | 15 | Rsi>=65 & Pcr 0.8-1.0 & Atr 0.04-0.05 & Normal: 14 (2) / +1,970 / 2.99; -1,470 |
| Trend | CALL | 27 | 6 | identical to EMA1m |
| EMA1m | PUT | 41 | 6 | Rsi 38-45 & Pcr 0.8-1.0 & bear & GapDown: 7 (1) / +1,520 / 6.43; -970 |
| EMA5m | PUT | 49 | 7 | Rsi 38-45 & Pcr 0.8-1.0 & Ema5m bear & GapDown: 6 (1) / +1,800 / inf; -490 |
| ATR | PUT | 71 | 11 | Rsi 38-45 & Pcr 1.0-1.2 & Atr>=0.05 & GapUp: 3 (1) / +600 / inf; all +310 PF 1.04 (train -1,430) |
| Trend | PUT | 41 | 6 | identical to EMA1m |

No Gap+RSI+PCR+X cell has more than 21 holdout trades. Trend=Bullish/Bearish duplicates the EMA1m alignment in this data.

## Conclusion
- Validated: none.
- Promising but failed: CALL Pcr<0.8 & Normal (+Ema5m bull); CALL Ema5m bear & GapUp (only family positive in all three splits, 25 holdout trades on 1 day); PUT Rsi 38-45 & Pcr 0.8-1.0 & GapDown. These positive holdouts cover 1-3 days and 6-26 trades — not evidence of profitability.
- No edge: every RSI band loses in both directions, including live PUT 28-38; neither RSI/PCR polarity helps under any gap; adding EMA/ATR/Trend does not rescue it.
- Limits: 23 days / 5-day holdout; Delta 87% missing; index-point barriers, not premium; overlapping intraday windows; ~5,300 cells searched, so many positive holdouts are expected by chance. Re-run at >= 40 sessions.
