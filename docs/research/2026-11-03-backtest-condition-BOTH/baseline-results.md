# Baseline (unfiltered base rate) — 2026-09-30..2026-11-03, 23 days

Source: grid.csv `BASE (no filter)` rows. Fixed index-point barrier outcomes from IndicatorSnapshotOutcomes (15-min window), one observation per symbol-minute-direction, Rs per lot after the grid's configured costs. **Not** an option-premium replay.

| Dir | Split | Trades | Days | Target% | Net | PF | Exp | MaxDD |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| CALL | Train (<10-21) | 1898 | 13 | 48.4 | -181,040 | 0.49 | -95.4 | 180,640 |
| CALL | Val (10-21..10-27) | 812 | 5 | 45.3 | -97,920 | 0.44 | -120.6 | 98,920 |
| CALL | Holdout (10-28..11-03) | 941 | 5 | 50.8 | -72,170 | 0.55 | -76.7 | 72,170 |
| PUT | Train | 1968 | 13 | 49.8 | -169,370 | 0.53 | -86.1 | 170,770 |
| PUT | Val | 1146 | 5 | 50.9 | -101,220 | 0.54 | -88.3 | 102,020 |
| PUT | Holdout | 583 | 5 | 47.3 | -60,490 | 0.46 | -103.8 | 61,290 |

Live-band reference cells (single-condition, same engine):

| Cell | Hold N | Hold Net | Hold PF | All Net | All PF |
|---|---:|---:|---:|---:|---:|
| PUT Rsi5m=28-38 (live PUT band) | 142 | -15,160 | 0.47 | -91,730 | 0.58 |
| CALL Rsi5m=55-65 | 391 | -34,630 | 0.51 | -158,970 | 0.47 |
| CALL Rsi5m=45-55 | 139 | -3,060 | 0.84 | -101,480 | 0.48 |
