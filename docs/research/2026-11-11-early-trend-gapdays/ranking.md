# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**Best validated: CALL|Any@0.30|A-Live** - validation net 25937.22, PF 1.57; held-out test net 37310.74, PF 1.51 on 46 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Any@0.30|A-Live | YES | 79 | 16205.28 | 1.21 | 41 | 25937.22 | 1.57 | 14237.7 | 46 | 37310.74 | 1.51 |
| 2 | CALL|Any@0.30|E-GapDays1030 | YES | 83 | 27296.31 | 1.35 | 43 | 21451.63 | 1.43 | 15923.68 | 47 | 42761.46 | 1.58 |
| 3 | CALL|GapDown@0.30|A-Live | no | 26 | 3073.88 | 1.11 | 19 | -4931.91 | 0.73 | 6934.83 | 3 | 8066.38 | NULL |
| 4 | CALL|GapDown@0.30|E-GapDays1030 | no | 30 | 14164.92 | 1.48 | 20 | -6617.88 | 0.66 | 8336.78 | 4 | 13517.11 | NULL |

## CALL SENSEX

**Best validated: CALL|Any@0.30|E-GapDays1030** - validation net 24318.35, PF 1.66; held-out test net 29759.87, PF 1.54 on 58 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Any@0.30|E-GapDays1030 | YES | 72 | 6308.86 | 1.14 | 54 | 24318.35 | 1.66 | 7755.77 | 58 | 29759.87 | 1.54 |
| 2 | CALL|Any@0.30|A-Live | YES | 71 | 7466.04 | 1.17 | 52 | 23204.28 | 1.66 | 7755.77 | 51 | 17437.96 | 1.34 |
| 3 | CALL|GapUp@0.30|E-GapDays1030 | YES | 18 | 11868.4 | 2.36 | 24 | 10176.76 | 1.55 | 7501.01 | 26 | 32426.85 | 2.61 |
| 4 | CALL|GapUp@0.30|A-Live | YES | 17 | 13025.59 | 2.71 | 22 | 9062.69 | 1.54 | 7501.01 | 19 | 20104.94 | 2.2 |

## CALL ALL

**Best validated: CALL|Any@0.30|E-GapDays1030** - validation net 40445.08, PF 1.6; held-out test net 44297.06, PF 1.44 on 71 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Any@0.30|E-GapDays1030 | YES | 119 | 39915.52 | 1.43 | 77 | 40445.08 | 1.6 | 13909.13 | 71 | 44297.06 | 1.44 |
| 2 | CALL|Any@0.30|A-Live | YES | 117 | 29568.06 | 1.32 | 75 | 39355.09 | 1.6 | 12223.16 | 66 | 32669.44 | 1.34 |
| 3 | CALL|GapUp@0.30|E-GapDays1030 | YES | 27 | 16952.71 | 1.99 | 27 | 22834.96 | 2.25 | 7501.01 | 29 | 30900.49 | 1.9 |
| 4 | CALL|GapUp@0.30|A-Live | YES | 26 | 18109.9 | 2.13 | 26 | 20058.99 | 2.1 | 7501.01 | 22 | 18578.58 | 1.6 |
| 5 | CALL|GapDown@0.30|A-Live | YES | 30 | 3955.76 | 1.14 | 29 | 110.57 | 1.01 | 10200.44 | 6 | 17466.86 | NULL |
| 6 | CALL|GapDown@0.30|E-GapDays1030 | no | 32 | 13668.35 | 1.46 | 30 | -1575.4 | 0.93 | 10200.44 | 5 | 19350.97 | NULL |

