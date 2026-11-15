# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 21 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**Best validated: CALL|Normal@0.30|B-GateOff** - validation net 15636.74, PF 1.4; held-out test net 59314.65, PF 1.62 on 86 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Normal@0.30|B-GateOff | YES | 80 | 816.91 | 1.01 | 32 | 15636.74 | 1.4 | 9305.8 | 86 | 59314.65 | 1.62 |
| 2 | CALL|Normal@0.30|A-Live | YES | 38 | 1382.87 | 1.04 | 15 | 13795.65 | 1.56 | 9305.8 | 29 | 22127.05 | 1.51 |
| 3 | CALL|GapUp@0.30|B-GateOff | no | 28 | -2584.95 | 0.91 | 22 | 17930.14 | 2.12 | 5974.61 | 43 | 54510.78 | 2.96 |
| 4 | CALL|Normal@0.30|C-0.20 | no | 45 | -7983.18 | 0.82 | 19 | 15682.03 | 1.58 | 9305.8 | 33 | 23550.72 | 1.48 |
| 5 | CALL|Normal@0.30|C-0.30 | no | 48 | -7054.44 | 0.85 | 20 | 15682.03 | 1.58 | 9305.8 | 37 | 27308.05 | 1.55 |
| 6 | CALL|Normal@0.30|C-0.50 | no | 51 | -7372.48 | 0.86 | 22 | 15652.96 | 1.54 | 9305.8 | 44 | 33841.73 | 1.62 |
| 7 | CALL|Normal@0.30|C-0.40 | no | 49 | -8617.64 | 0.83 | 21 | 13672.59 | 1.47 | 9305.8 | 39 | 23651.65 | 1.45 |
| 8 | CALL|Normal@0.30|C-0.10 | no | 43 | -5063.29 | 0.88 | 18 | 11098.7 | 1.41 | 9305.8 | 32 | 18947.76 | 1.39 |
| 9 | CALL|GapDown@0.30|A-Live | no | 26 | 3073.88 | 1.11 | 19 | -4931.91 | 0.73 | 6934.83 | 3 | 8066.38 | NULL |
| 10 | CALL|GapDown@0.30|C-0.10 | no | 26 | 3073.88 | 1.11 | 19 | -4931.91 | 0.73 | 6934.83 | 3 | 8066.38 | NULL |

## CALL SENSEX

**Best validated: CALL|GapDown@0.30|B-GateOff** - validation net 9673.15, PF 2.17; held-out test net -1303.38, PF 0.87 on 13 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapDown@0.30|B-GateOff | YES | 20 | 362.7 | 1.02 | 18 | 9673.15 | 2.17 | 4785.54 | 13 | -1303.38 | 0.87 |
| 2 | CALL|GapDown@0.30|C-0.50 | YES | 15 | 1356.73 | 1.12 | 16 | 7512.15 | 1.91 | 4785.54 | 9 | 17.93 | 1 |
| 3 | CALL|GapUp@0.30|B-GateOff | YES | 34 | 11532.92 | 1.61 | 32 | 6235.92 | 1.23 | 8727.03 | 72 | 34274.17 | 1.64 |
| 4 | CALL|Normal@0.30|C-0.30 | no | 51 | -10.45 | 1 | 17 | 15355.54 | 2.5 | 3904.37 | 29 | -4910.26 | 0.86 |
| 5 | CALL|Normal@0.30|C-0.40 | no | 52 | -10.45 | 1 | 18 | 14243.43 | 2.26 | 5016.49 | 29 | -4910.26 | 0.86 |
| 6 | CALL|Normal@0.30|C-0.50 | no | 53 | -1263.89 | 0.96 | 18 | 14243.43 | 2.26 | 5016.49 | 29 | -4910.26 | 0.86 |
| 7 | CALL|Normal@0.30|B-GateOff | no | 81 | -9833.07 | 0.83 | 34 | 13074.77 | 1.56 | 11698.33 | 130 | 54165.75 | 1.55 |
| 8 | CALL|Normal@0.30|C-0.10 | no | 42 | -6041.72 | 0.79 | 16 | 11643.3 | 2.14 | 3904.37 | 26 | -5088.76 | 0.83 |
| 9 | CALL|Normal@0.30|C-0.20 | no | 45 | -962.68 | 0.97 | 16 | 11643.3 | 2.14 | 3904.37 | 28 | -1058.53 | 0.97 |
| 10 | CALL|Normal@0.30|A-Live | no | 40 | -4260.43 | 0.84 | 15 | 8765.55 | 1.86 | 3904.37 | 24 | -4459.67 | 0.85 |

## CALL ALL

**Best validated: CALL|GapUp@0.30|B-GateOff** - validation net 30742.32, PF 2; held-out test net 47405.81, PF 1.81 on 73 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUp@0.30|B-GateOff | YES | 54 | 7178.64 | 1.18 | 45 | 30742.32 | 2 | 8727.03 | 73 | 47405.81 | 1.81 |
| 2 | CALL|Normal@0.30|A-Live | YES | 70 | 2170.38 | 1.04 | 27 | 26737.78 | 1.89 | 7554.02 | 45 | -7.63 | 1 |
| 3 | CALL|GapUp@0.30|C-0.40 | YES | 16 | 4558.69 | 1.42 | 25 | 14661.7 | 1.75 | 8727.03 | 18 | 20809.1 | 2.46 |
| 4 | CALL|GapUp@0.30|C-0.50 | YES | 19 | 9057.33 | 1.84 | 25 | 14661.7 | 1.75 | 8727.03 | 20 | 18967.45 | 2.18 |
| 5 | CALL|GapDown@0.30|A-Live | YES | 30 | 3955.76 | 1.14 | 29 | 110.57 | 1.01 | 10200.44 | 6 | 17466.86 | NULL |
| 6 | CALL|GapDown@0.30|C-0.10 | YES | 30 | 3955.76 | 1.14 | 29 | 110.57 | 1.01 | 10200.44 | 7 | 23200.08 | NULL |
| 7 | CALL|GapDown@0.30|C-0.20 | YES | 31 | 8856.94 | 1.3 | 29 | 110.57 | 1.01 | 10200.44 | 7 | 23200.08 | NULL |
| 8 | CALL|Normal@0.30|B-GateOff | no | 139 | -23278.17 | 0.82 | 60 | 35828.55 | 1.67 | 13577.19 | 168 | 61649.81 | 1.35 |
| 9 | CALL|Normal@0.30|C-0.30 | no | 85 | -6947.28 | 0.9 | 35 | 34624.78 | 2.08 | 6895.34 | 60 | 20020.58 | 1.23 |
| 10 | CALL|Normal@0.30|C-0.50 | no | 89 | -7265.32 | 0.9 | 38 | 33483.59 | 1.95 | 6895.34 | 69 | 28000.23 | 1.29 |

