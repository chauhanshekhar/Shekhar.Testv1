# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 3, net 13405.36, PF NULL, validation net 2399.12 (PF NULL, 1 trades), test net 11006.24 (PF NULL, 2 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 2 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GUM|Full | no | 2 | 452.74 | 1.2 | 1 | 2974.4 | NULL | 0 | 3 | -2113.62 | 0.6 |
| 2 | CALL|GUM|NoConfirm | no | 2 | 452.74 | 1.2 | 2 | 2717.62 | NULL | 0 | 5 | -4961.92 | 0.4 |

## CALL BANKNIFTY

Baseline: trades 9, net 12118.77, PF 2.87, validation net 2423.91 (PF 1.9, 3 trades), test net 10647.7 (PF NULL, 2 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 4 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GUM|Baseline | no | 4 | -952.84 | 0.75 | 3 | 2423.91 | 1.9 | 2692.13 | 2 | 10647.7 | NULL |
| 2 | CALL|GUM|NoConfirm | no | 9 | 5171.46 | 1.47 | 2 | 0 | NULL | 0 | 7 | 21009.22 | 11.47 |
| 3 | CALL|GUM|Full | no | 10 | 3099.3 | 1.41 | 2 | -2792.84 | 0 | 2792.84 | 7 | 6772.24 | 2.05 |
| 4 | CALL|GUM|Rsi45-65 | no | 5 | -8265.15 | 0 | 1 | -2792.84 | 0 | 2792.84 | 6 | 7353.1 | 3.2 |

## CALL SENSEX

Baseline: trades 33, net 23994.19, PF 2.07, validation net 5920.06 (PF 1.34, 20 trades), test net 14234.27 (PF 4.57, 8 trades).
**Best validated: CALL|GUM|NoConfirm** - validation net 8349.38, PF 7.34; held-out test net -9226.71, PF 0.28 on 8 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GUM|NoConfirm | YES | 11 | 8789.37 | 2.78 | 7 | 8349.38 | 7.34 | 1316.4 | 8 | -9226.71 | 0.28 |
| 2 | CALL|GUM|Baseline | YES | 5 | 3839.86 | 4.19 | 20 | 5920.06 | 1.34 | 6238.1 | 8 | 14234.27 | 4.57 |
| 3 | CALL|GUM|Full | YES | 11 | 6318.19 | 2.62 | 6 | 3328.81 | 2.11 | 3009.12 | 6 | -257.86 | 0.96 |
| 4 | CALL|GUM|Rsi45-65 | YES | 5 | 368.08 | 1.15 | 4 | 1310.94 | 1.44 | 3009.12 | 3 | -2079.61 | 0.36 |

## CALL ALL

Baseline: trades 40, net 31622.82, PF 2.14, validation net 6998.11 (PF 1.37, 22 trades), test net 21737.69 (PF 6.45, 9 trades).
**Best validated: CALL|GUM|Baseline** - validation net 6998.11, PF 1.37; held-out test net 21737.69, PF 6.45 on 9 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GUM|Baseline | YES | 9 | 2887.02 | 1.58 | 22 | 6998.11 | 1.37 | 6238.1 | 9 | 21737.69 | 6.45 |
| 2 | CALL|GUM|NoConfirm | YES | 21 | 10928.53 | 1.87 | 9 | 2928.45 | 1.71 | 2792.84 | 16 | 15931.26 | 2.18 |
| 3 | CALL|GUM|Full | no | 22 | 1123.41 | 1.08 | 7 | -185.75 | 0.98 | 4471.08 | 14 | 795.48 | 1.05 |
| 4 | CALL|GUM|Rsi45-65 | no | 10 | -7897.07 | 0.27 | 5 | -1481.9 | 0.74 | 3009.12 | 9 | 5273.48 | 1.8 |

