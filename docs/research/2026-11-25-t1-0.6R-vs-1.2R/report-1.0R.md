# T1 at 1.0R vs current 1.2R (2026-11-25)

Same method as `report.md` (0.6R test): `--backtest-trades --from 2026-10-14`, live settings, costs included.
Override: `--t1-mult 1.25` (1.25 / 1.25 stop multiplier = 1.00R). Outputs: `docs/research/2026-11-25-t1-1.0R/`.

| | Trades | Win % | Net Rs | PF | Exp/trade | Max DD |
|---|---:|---:|---:|---:|---:|---:|
| T1 1.2R (current) | 89 | 47% | +52,610 | 1.76 | +591 | 14,588 |
| T1 1.0R | 90 | 47% | +42,265 | 1.61 | +470 | 14,903 |

- Both halves worse: first +9,690 -> +5,679 (PF 1.31 -> 1.18); second +42,920 -> +36,586 (PF 2.12 -> 1.95).
- BANKNIFTY +20,763 -> +15,505; SENSEX +21,818 -> +17,611; NIFTY +10,030 -> +9,149; CALL +53,323 -> +45,186; PUT -712 -> -2,921.
- 1.0R better on 0 of 27 days, worse on 21, equal on 6. 89 of 90 entries identical (matched net +52,610 -> +42,385).
- Trend across the three tests: lower T1 = lower net (0.6R +23,763, 1.0R +42,265, 1.2R +52,610). Higher values not yet tested.
- Same caveats as the 0.6R report (current settings over old days, one premium sample per minute, 27 sessions).
