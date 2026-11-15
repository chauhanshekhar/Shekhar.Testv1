# /backtest-condition PUT — 2026-11-03 (discovery + execution parity)

## Stage 1 — Condition discovery (simulated only)
- `combo_grid.py --dir PUT --window 15 --max-k 4 --min-train 100` -> grid.csv (1,939 cells; 1,347 after removing Delta=Missing duplicates, Delta is 87% missing). `live_variants.py --dir PUT` -> variants.csv (249 variants of the live rule).
- 23 days 2026-09-30..2026-11-03; train 13 d (to 10-20), validation 5 d (10-21..10-27), holdout 5 d (10-28..11-03). 1,087,364 aligned outcome rows (0 orphans) -> 23,813 deduped.
- Index-point barriers (15-min window), delta 0.5, Rs 20/order + 1 pt slippage, Rs/lot. Not option premium.
- Criteria: train N >= 100 and net > 0; val net > 0; holdout N >= 30, >= 5 days, net > 0, PF > 1.2.

| Class | Cells |
|---|---:|
| VALIDATED HISTORICALLY | 0 |
| PROMISING | 0 |
| FAILED | 861 |
| INSUFFICIENT DATA | 486 |

151 cells meet the size rules (train >= 100, holdout >= 30 trades on 5 days); none has a positive holdout. Best: `Atr<0.03` holdout 44 tr / 5 d / -740 / PF 0.86 (all -24,970, PF 0.47).

Baselines: no filter all -331,080 (PF 0.52, holdout 583 tr -60,490 PF 0.46); Rsi5m 28-38 all -91,730 (PF 0.58, holdout 142 tr -15,160 PF 0.47); full live rule (live_variants) 812 tr, all -72,470 PF 0.53, holdout 127 tr -14,460. 0 of 249 live variants pass; 49 beat live in all three periods but are still negative.

Robustness (robustness.csv): base 0/6 positive weeks, 0/23 positive days; live band 0/6 weeks, 0/20 days; Atr<0.03 0/6 weeks, 7/22 days. No result depends on one day.

## Stage 2 — Execution parity (actual trades used for comparison only)
Tolerances fixed in put_parity.py before the run. 154 actual PUT signals (CLOSED, 09-30..11-03).

| Status | n |
|---|---:|
| MATCHED | 149 (97%) |
| ENTRY_MISMATCH (> 0.10% index price) | 3 (median 0.11%) |
| ACTUAL_ONLY (no snapshot minute) | 2 |
| SIMULATION_ONLY | not applicable (simulation scores every minute, not discrete signals) |

Matched: median entry diff 0.018%. Actual per-lot P&L +2,985 vs simulated +6,180. Exit-class agreement 62%.

| Actual \ Sim | SL | WIN | OTHER |
|---|---:|---:|---:|
| SL | 31 | **51** | 0 |
| WIN | 3 | 61 | 1 |
| OTHER | 0 | 2 | 0 |

Main gap: 51 trades stopped out live while the index-barrier sim hit target. OPTION_SL_HIT 44 trades: actual -24,632 per lot vs sim -2,080 — the live option stop sits inside the sim's index stop. Gains are also understated: PROTECTED_SL_HIT actual +25,140 vs sim +11,080. By symbol: BANKNIFTY actual -795 vs sim +3,700; SENSEX +4,358 vs +3,080; NIFTY -577 vs -600.

**Classification: PARTIAL PARITY.** Entries match well; exits do not, because the sim has no option-premium stop, T1 partial, protected or trailing SL. A premium replay is blocked because OptionMinuteHistory was truncated on 10-07.

Caveats: TradeJournal for 09-30 is restated (4 lots, unfilled exits); per-lot = journal P&L / Signal.Quantity; journal costs are not verified.

## Decision
| Discovery | Parity | Decision |
|---|---|---|
| FAILED (all 861 sized cells) / INSUFFICIENT (486) | PARTIAL | Reject candidates; collect more data and fix simulation exits |

- Historically good: nothing passes. Live-gated PUT signals simulate better (+6,180 on 149 trades) than any unfiltered grid cell, which means the live gates not modelled in the grid add selection value.
- Reproduces execution: entries yes; exits no.
- Ready for paper: nothing new.
- Unproven: everything.

Next step: replay live PUT signals with the live option-premium exit ladder (option SL, T1 partial, protected/trailing SL), once OptionMinuteHistory has >= 2 weeks of data again.
