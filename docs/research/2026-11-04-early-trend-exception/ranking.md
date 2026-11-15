# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**Best validated: CALL|GapUp@0.30|B-GateOff** - validation net 586.98, PF 1.13; held-out test net 11603.35, PF 5.32 on 5 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUp@0.30|B-GateOff | YES | 2 | 452.74 | 1.2 | 5 | 586.98 | 1.13 | 4529.77 | 5 | 11603.35 | 5.32 |
| 2 | CALL|Normal@0.30|B-GateOff | no | 2 | 458.64 | 1.2 | 6 | -1594.14 | 0.77 | 4529.77 | 30 | -1279.58 | 0.97 |

## CALL BANKNIFTY

**Best validated: CALL|Normal@0.30|B-GateOff** - validation net 6779.31, PF 1.2; held-out test net 28681.9, PF 1.27 on 81 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Normal@0.30|B-GateOff | YES | 72 | 1883.91 | 1.03 | 28 | 6779.31 | 1.2 | 14702.74 | 81 | 28681.9 | 1.27 |
| 2 | CALL|Normal@0.30|A-Live | YES | 33 | 1279.63 | 1.04 | 13 | 1256.72 | 1.06 | 11804.31 | 34 | 27723.56 | 1.55 |
| 3 | CALL|Normal@0.30|C-0.30 | no | 42 | -8880.04 | 0.8 | 16 | 7571.81 | 1.35 | 11804.31 | 45 | 30207.61 | 1.51 |
| 4 | CALL|Normal@0.30|C-0.50 | no | 44 | -7560.83 | 0.83 | 19 | 5908.53 | 1.24 | 13440.84 | 51 | 34700.75 | 1.54 |
| 5 | CALL|Normal@0.30|C-0.20 | no | 40 | -8063.77 | 0.8 | 15 | 5828.78 | 1.27 | 11804.31 | 40 | 26450.28 | 1.45 |
| 6 | CALL|Normal@0.30|C-0.40 | no | 43 | -10439.46 | 0.77 | 17 | 5567.23 | 1.24 | 11804.31 | 47 | 26551.21 | 1.42 |
| 7 | CALL|GapUp@0.30|A-Live | no | 4 | -952.84 | 0.75 | 3 | 2423.91 | 1.9 | 2692.13 | 2 | 10647.7 | NULL |
| 8 | CALL|GapUp@0.30|C-0.10 | no | 4 | -952.84 | 0.75 | 3 | 2423.91 | 1.9 | 2692.13 | 3 | 13956.13 | NULL |
| 9 | CALL|Normal@0.30|C-0.10 | no | 38 | -5150.93 | 0.87 | 14 | 1256.72 | 1.06 | 11804.31 | 39 | 21847.32 | 1.37 |
| 10 | CALL|GapUp@0.30|C-0.20 | no | 4 | -952.84 | 0.75 | 4 | -368.92 | 0.93 | 2937.77 | 3 | 13956.13 | NULL |

## CALL SENSEX

**Best validated: CALL|Normal@0.30|C-0.30** - validation net 11859.04, PF 2.85; held-out test net 1533.33, PF 1.04 on 30 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Normal@0.30|C-0.30 | YES | 51 | 1320.1 | 1.04 | 12 | 11859.04 | 2.85 | 3906.76 | 30 | 1533.33 | 1.04 |
| 2 | CALL|Normal@0.30|C-0.40 | YES | 52 | 1320.1 | 1.04 | 12 | 11859.04 | 2.85 | 3906.76 | 31 | 410.27 | 1.01 |
| 3 | CALL|Normal@0.30|C-0.50 | YES | 53 | 54.33 | 1 | 12 | 11859.04 | 2.85 | 3906.76 | 31 | 410.27 | 1.01 |
| 4 | CALL|GapDown@0.30|B-GateOff | YES | 20 | 361.4 | 1.02 | 18 | 9759.04 | 2.17 | 4832.63 | 13 | -1303.38 | 0.87 |
| 5 | CALL|Normal@0.30|C-0.20 | YES | 45 | 358.5 | 1.01 | 11 | 8110.27 | 2.27 | 3906.76 | 29 | 5422.96 | 1.17 |
| 6 | CALL|GapDown@0.30|C-0.50 | YES | 15 | 1365.43 | 1.12 | 16 | 7576.78 | 1.91 | 4832.63 | 9 | 17.93 | 1 |
| 7 | CALL|GapUp@0.30|B-GateOff | YES | 32 | 10859.44 | 1.63 | 34 | 7074.51 | 1.24 | 8813.26 | 32 | 20340.81 | 2.02 |
| 8 | CALL|GapUp@0.30|C-0.10 | YES | 5 | 3839.86 | 4.19 | 22 | 6934.14 | 1.37 | 7575.18 | 9 | 16812.67 | 5.21 |
| 9 | CALL|GapUp@0.30|C-0.20 | YES | 7 | 1026.43 | 1.26 | 25 | 6821.09 | 1.32 | 8813.26 | 10 | 18795.03 | 5.71 |
| 10 | CALL|GapUp@0.30|C-0.30 | YES | 7 | 1026.43 | 1.26 | 25 | 6821.09 | 1.32 | 8813.26 | 10 | 18795.03 | 5.71 |

## CALL ALL

**Best validated: CALL|Normal@0.30|A-Live** - validation net 9612.24, PF 1.35; held-out test net 10325.9, PF 1.13 on 51 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Normal@0.30|A-Live | YES | 65 | 3392.85 | 1.07 | 24 | 9612.24 | 1.35 | 10086.85 | 51 | 10325.9 | 1.13 |
| 2 | CALL|GapUp@0.30|B-GateOff | YES | 52 | 6489.69 | 1.17 | 32 | 8710.18 | 1.35 | 8813.26 | 46 | 47200.13 | 2.67 |
| 3 | CALL|GapUp@0.30|C-0.20 | YES | 11 | 73.59 | 1.01 | 26 | 7226.23 | 1.34 | 8813.26 | 11 | 26298.45 | 7.59 |
| 4 | CALL|GapUp@0.30|C-0.30 | YES | 12 | 3395.12 | 1.44 | 26 | 7226.23 | 1.34 | 8813.26 | 11 | 26298.45 | 7.59 |
| 5 | CALL|GapUp@0.30|C-0.40 | YES | 14 | 3810.13 | 1.41 | 26 | 7226.23 | 1.34 | 8813.26 | 13 | 19468.86 | 2.8 |
| 6 | CALL|GapUp@0.30|C-0.50 | YES | 17 | 8353.03 | 1.91 | 26 | 7226.23 | 1.34 | 8813.26 | 13 | 19468.86 | 2.8 |
| 7 | CALL|GapUp@0.30|A-Live | YES | 9 | 2887.02 | 1.58 | 22 | 6998.11 | 1.37 | 6238.1 | 9 | 21737.69 | 6.45 |
| 8 | CALL|GapUp@0.30|C-0.10 | YES | 9 | 2887.02 | 1.58 | 24 | 5661.04 | 1.28 | 7575.18 | 10 | 24316.09 | 7.09 |
| 9 | CALL|GapDown@0.30|A-Live | YES | 30 | 3954.45 | 1.14 | 28 | 242.84 | 1.01 | 10215.79 | 7 | 17466.86 | NULL |
| 10 | CALL|GapDown@0.30|C-0.10 | YES | 30 | 3954.45 | 1.14 | 28 | 242.84 | 1.01 | 10215.79 | 8 | 23200.08 | NULL |

