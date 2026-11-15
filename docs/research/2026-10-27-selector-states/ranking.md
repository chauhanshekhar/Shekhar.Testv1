# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 0, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 0 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 20, net 9081.95, PF 1.33, validation net -11108.68 (PF 0, 5 trades), test net 20190.63 (PF 2.2, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL SENSEX

Baseline: trades 11, net 22814.64, PF 6.57, validation net 5582.1 (PF NULL, 2 trades), test net 18402.65 (PF 7.29, 8 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

Baseline: trades 28, net 21851.28, PF 1.68, validation net -5526.58 (PF 0.5, 7 trades), test net 28547.97 (PF 2.45, 20 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT NIFTY

Baseline: trades 21, net 10585.1, PF 1.44, validation net -4804.25 (PF 0, 3 trades), test net 10772.27 (PF 1.63, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 196, net 47874.19, PF 1.26, validation net 15855.23 (PF 1.95, 27 trades), test net -2478.06 (PF 0.96, 57 trades).
**Best validated: PUT|SEL|AllowRange** - validation net 19604.78, PF 2.05; held-out test net 18412.39, PF 1.27 on 64 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|SEL|AllowRange | YES | 121 | 43445.72 | 1.41 | 30 | 19604.78 | 2.05 | 8409.25 | 64 | 18412.39 | 1.27 |
| 2 | PUT|SEL|AllowReversal+Range | YES | 125 | 44675.44 | 1.41 | 30 | 19604.78 | 2.05 | 8409.25 | 65 | 21335.87 | 1.31 |
| 3 | PUT|SEL|Baseline | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 4 | PUT|SEL|AllowReversal | YES | 119 | 36592.6 | 1.34 | 28 | 13994.94 | 1.75 | 8374.47 | 60 | 10147.47 | 1.15 |

## PUT SENSEX

Baseline: trades 89, net 47326.82, PF 1.92, validation net 1092.31 (PF 1.33, 5 trades), test net 37417.49 (PF 1.81, 75 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 4 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 261, net 82487.58, PF 1.37, validation net 14412.38 (PF 1.64, 34 trades), test net 37107.91 (PF 1.39, 109 trades).
**Best validated: PUT|SEL|AllowRange** - validation net 18161.92, PF 1.74; held-out test net 51768.33, PF 1.53 on 116 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|SEL|AllowRange | YES | 128 | 44267.96 | 1.41 | 37 | 18161.92 | 1.74 | 8409.25 | 116 | 51768.33 | 1.53 |
| 2 | PUT|SEL|AllowReversal+Range | YES | 132 | 45833.54 | 1.41 | 37 | 18161.92 | 1.74 | 8409.25 | 121 | 55869.13 | 1.53 |
| 3 | PUT|SEL|Baseline | YES | 118 | 30967.3 | 1.29 | 34 | 14412.38 | 1.64 | 6514.18 | 109 | 37107.91 | 1.39 |
| 4 | PUT|SEL|AllowReversal | YES | 125 | 31482.1 | 1.29 | 35 | 12552.08 | 1.51 | 8374.47 | 116 | 58595.82 | 1.6 |

