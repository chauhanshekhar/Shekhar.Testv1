# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 3, net 13405.36, PF NULL, validation net 2399.12 (PF NULL, 1 trades), test net 11006.24 (PF NULL, 2 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 9, net 12118.77, PF 2.87, validation net 2423.91 (PF 1.9, 3 trades), test net 10647.7 (PF NULL, 2 trades).
**Best validated: CALL|GapUp@0.30|FastEma** - validation net 2423.91, PF 1.9; held-out test net 10647.7, PF NULL on 2 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUp@0.30|FastEma | YES | 3 | 817.18 | 1.41 | 3 | 2423.91 | 1.9 | 2692.13 | 2 | 10647.7 | NULL |
| 2 | CALL|Normal@0.30|Baseline | YES | 33 | 1279.63 | 1.04 | 13 | 1256.72 | 1.06 | 11804.31 | 34 | 27723.56 | 1.55 |
| 3 | CALL|Normal@0.30|FastEma | YES | 33 | 1279.63 | 1.04 | 13 | 1256.72 | 1.06 | 11804.31 | 33 | 28570.69 | 1.62 |
| 4 | CALL|GapUp@0.30|Baseline | no | 4 | -952.84 | 0.75 | 3 | 2423.91 | 1.9 | 2692.13 | 2 | 10647.7 | NULL |
| 5 | CALL|GapDown@0.30|Baseline | no | 26 | 3068.6 | 1.11 | 19 | -4920.7 | 0.73 | 6918.05 | 3 | 8066.38 | NULL |
| 6 | CALL|GapDown@0.30|FastEma | no | 25 | 3068.6 | 1.11 | 19 | -4920.7 | 0.73 | 6918.05 | 3 | 3945.66 | 3.01 |

## CALL SENSEX

Baseline: trades 33, net 23994.19, PF 2.07, validation net 5920.06 (PF 1.34, 20 trades), test net 14234.27 (PF 4.57, 8 trades).
**Best validated: CALL|GapUp@0.30|FastEma** - validation net 10197.72, PF 1.79; held-out test net 14234.27, PF 4.57 on 8 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUp@0.30|FastEma | YES | 5 | 3839.86 | 4.19 | 18 | 10197.72 | 1.79 | 4848.69 | 8 | 14234.27 | 4.57 |
| 2 | CALL|GapUp@0.30|Baseline | YES | 5 | 3839.86 | 4.19 | 20 | 5920.06 | 1.34 | 6238.1 | 8 | 14234.27 | 4.57 |
| 3 | CALL|GapDown@0.30|FastEma | no | 14 | -3025.26 | 0.73 | 14 | 6588.75 | 1.92 | 4832.63 | 8 | 1868.67 | 1.33 |
| 4 | CALL|Normal@0.30|FastEma | no | 39 | -3383.13 | 0.86 | 9 | 6361.44 | 2.21 | 2749.5 | 25 | 4694.22 | 1.18 |
| 5 | CALL|GapDown@0.30|Baseline | no | 14 | -1316.55 | 0.88 | 15 | 5419.43 | 1.65 | 4832.63 | 8 | 1792.69 | 1.31 |
| 6 | CALL|Normal@0.30|Baseline | no | 40 | -2971.69 | 0.88 | 10 | 5204.2 | 1.81 | 3906.76 | 27 | 1999.55 | 1.07 |

## CALL ALL

Baseline: trades 40, net 31622.82, PF 2.14, validation net 6998.11 (PF 1.37, 22 trades), test net 21737.69 (PF 6.45, 9 trades).
**Best validated: CALL|GapUp@0.30|FastEma** - validation net 11275.78, PF 1.78; held-out test net 21737.69, PF 6.45 on 9 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUp@0.30|FastEma | YES | 8 | 4657.05 | 2.45 | 20 | 11275.78 | 1.78 | 4848.69 | 9 | 21737.69 | 6.45 |
| 2 | CALL|Normal@0.30|FastEma | YES | 64 | 2981.42 | 1.06 | 23 | 10769.48 | 1.4 | 8929.61 | 47 | 21779.7 | 1.31 |
| 3 | CALL|Normal@0.30|Baseline | YES | 65 | 3392.85 | 1.07 | 24 | 9612.24 | 1.35 | 10086.85 | 51 | 10325.9 | 1.13 |
| 4 | CALL|GapUp@0.30|Baseline | YES | 9 | 2887.02 | 1.58 | 22 | 6998.11 | 1.37 | 6238.1 | 9 | 21737.69 | 6.45 |
| 5 | CALL|GapDown@0.30|FastEma | YES | 29 | 3954.45 | 1.14 | 27 | 1412.15 | 1.07 | 10215.79 | 8 | 12016.58 | 4.65 |
| 6 | CALL|GapDown@0.30|Baseline | YES | 30 | 3954.45 | 1.14 | 28 | 242.84 | 1.01 | 10215.79 | 7 | 17466.86 | NULL |

## PUT NIFTY

Baseline: trades 8, net -3109.16, PF 0.74, validation net -4342.62 (PF 0.38, 4 trades), test net 1233.45 (PF 1.25, 4 trades).
**Best validated: PUT|GapDown@0.30|Baseline** - validation net 2175.77, PF 1.48; held-out test net -2200.09, PF 0.62 on 5 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapDown@0.30|Baseline | YES | 4 | 7530.67 | 4.32 | 4 | 2175.77 | 1.48 | 4558.08 | 5 | -2200.09 | 0.62 |
| 2 | PUT|GapDown@0.30|FastEma | YES | 4 | 7530.67 | 4.32 | 4 | 2175.77 | 1.48 | 4558.08 | 5 | -2200.09 | 0.62 |
| 3 | PUT|Normal@0.30|Baseline | no | 1 | -2264.89 | 0 | 1 | 0 | NULL | 0 | 12 | 13689.41 | 1.99 |
| 4 | PUT|Normal@0.30|FastEma | no | 1 | -2264.89 | 0 | 1 | 0 | NULL | 0 | 12 | 13689.41 | 1.99 |

## PUT BANKNIFTY

Baseline: trades 49, net 35891.81, PF 1.89, validation net 17721.05 (PF 6.98, 7 trades), test net 3565.12 (PF 1.27, 14 trades).
**Best validated: PUT|GapUp@0.30|Baseline** - validation net 17721.05, PF 6.98; held-out test net 3565.12, PF 1.27 on 14 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapUp@0.30|Baseline | YES | 28 | 14605.64 | 1.61 | 7 | 17721.05 | 6.98 | 2962.39 | 14 | 3565.12 | 1.27 |
| 2 | PUT|GapUp@0.30|FastEma | YES | 27 | 13431.1 | 1.58 | 7 | 14039.25 | 5.74 | 2962.39 | 14 | 3565.12 | 1.27 |
| 3 | PUT|GapDown@0.30|Baseline | YES | 39 | 20429.28 | 1.63 | 6 | 11950.21 | 6.47 | 2184.32 | 1 | -4123.33 | 0 |
| 4 | PUT|GapDown@0.30|FastEma | YES | 38 | 21436.97 | 1.7 | 6 | 11950.21 | 6.47 | 2184.32 | 1 | -4123.33 | 0 |
| 5 | PUT|Normal@0.30|Baseline | YES | 57 | 3145.58 | 1.06 | 21 | 9868.55 | 1.61 | 7719.69 | 59 | -2386.12 | 0.97 |
| 6 | PUT|Normal@0.30|FastEma | no | 57 | -876.92 | 0.98 | 20 | 6923.22 | 1.43 | 7719.69 | 58 | -847.75 | 0.99 |

## PUT SENSEX

Baseline: trades 43, net 30923.1, PF 2.31, validation net 6074.14 (PF 1.74, 15 trades), test net 24582.09 (PF 2.6, 26 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 6 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapDown@0.30|FastEma | no | 2 | 3128.02 | NULL | 11 | 12511.4 | 2.73 | 2662.09 | 5 | 674.87 | 1.14 |
| 2 | PUT|GapDown@0.30|Baseline | no | 2 | 3128.02 | NULL | 13 | 8689.95 | 1.79 | 4625.98 | 5 | 674.87 | 1.14 |
| 3 | PUT|GapUp@0.30|FastEma | no | 2 | 266.87 | NULL | 14 | 7268.2 | 2.07 | 1712.78 | 25 | 22248.95 | 2.4 |
| 4 | PUT|GapUp@0.30|Baseline | no | 2 | 266.87 | NULL | 15 | 6074.14 | 1.74 | 2773.11 | 26 | 24582.09 | 2.6 |
| 5 | PUT|Normal@0.30|Baseline | no | 7 | 9213.94 | NULL | 1 | -1117.22 | 0 | 1117.22 | 62 | 20549.72 | 1.51 |
| 6 | PUT|Normal@0.30|FastEma | no | 6 | 7849.24 | NULL | 1 | -1117.22 | 0 | 1117.22 | 60 | 28321.45 | 1.77 |

## PUT ALL

Baseline: trades 84, net 60508.69, PF 1.94, validation net 17459 (PF 2.28, 19 trades), test net 28177.17 (PF 2.06, 35 trades).
**Best validated: PUT|GapDown@0.30|FastEma** - validation net 21967.21, PF 2.88; held-out test net -251.4, PF 0.96 on 5 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapDown@0.30|FastEma | YES | 42 | 19993.52 | 1.59 | 18 | 21967.21 | 2.88 | 4926.98 | 5 | -251.4 | 0.96 |
| 2 | PUT|GapDown@0.30|Baseline | YES | 43 | 18985.83 | 1.53 | 19 | 20109.65 | 2.49 | 4926.98 | 5 | -251.4 | 0.96 |
| 3 | PUT|GapUp@0.30|Baseline | YES | 30 | 14872.51 | 1.62 | 19 | 17459 | 2.28 | 5118.83 | 35 | 28177.17 | 2.06 |
| 4 | PUT|GapUp@0.30|FastEma | YES | 29 | 13697.97 | 1.59 | 18 | 13222.29 | 2 | 5118.83 | 33 | 28270.35 | 2.14 |
| 5 | PUT|Normal@0.30|Baseline | YES | 63 | 9976.65 | 1.2 | 23 | 8751.33 | 1.5 | 8836.52 | 96 | 28123.11 | 1.31 |
| 6 | PUT|Normal@0.30|FastEma | YES | 62 | 4942.97 | 1.1 | 22 | 5806.01 | 1.33 | 8836.52 | 93 | 39530.71 | 1.46 |

