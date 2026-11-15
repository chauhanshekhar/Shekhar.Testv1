# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|Stretch|Max0.85** - validation net 21992.69, PF 2.81; held-out test net 23284.71, PF 2.85 on 19 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Stretch|Max0.85 | YES | 38 | 9940.31 | 1.27 | 21 | 21992.69 | 2.81 | 2755 | 19 | 23284.71 | 2.85 |
| 2 | PUT|Stretch|Max0.95 | YES | 38 | 12447.43 | 1.36 | 22 | 21746.21 | 2.79 | 2755 | 19 | 23284.71 | 2.85 |
| 3 | PUT|Stretch|Max0.75 | YES | 38 | 9947.74 | 1.27 | 21 | 21251.32 | 2.75 | 2755 | 18 | 18695.18 | 2.49 |
| 4 | PUT|Stretch|Max0.65 | YES | 36 | 15539.22 | 1.47 | 19 | 18413.4 | 2.75 | 2755 | 15 | 21242.2 | 3.74 |
| 5 | PUT|Stretch|Max0.60 | YES | 33 | 1778.86 | 1.05 | 18 | 15734.62 | 2.5 | 2720.37 | 15 | 21242.2 | 3.74 |
| 6 | PUT|Stretch|Max1.10 | YES | 40 | 8405.91 | 1.23 | 26 | 13305.91 | 1.75 | 3836.1 | 21 | 19263.67 | 2.24 |
| 7 | PUT|Stretch|Min1.00 | no | 44 | 2493.95 | 1.05 | 27 | -617.58 | 0.97 | 7056.53 | 26 | 11568.12 | 1.44 |
| 8 | PUT|Stretch|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|Stretch|Max0.85** - validation net 23333.51, PF 2.7; held-out test net 25747.65, PF 2.17 on 39 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Stretch|Max0.85 | YES | 39 | 15766.83 | 1.43 | 25 | 23333.51 | 2.7 | 3500.11 | 39 | 25747.65 | 2.17 |
| 2 | PUT|Stretch|Max0.95 | YES | 39 | 17288.84 | 1.51 | 26 | 23087.04 | 2.69 | 3495.89 | 41 | 23137.96 | 1.94 |
| 3 | PUT|Stretch|Max0.75 | YES | 39 | 15774.26 | 1.43 | 25 | 22594.27 | 2.65 | 3452.91 | 39 | 25881.92 | 2.18 |
| 4 | PUT|Stretch|Max1.10 | YES | 41 | 11866.18 | 1.33 | 28 | 20777.45 | 2.38 | 3513.92 | 44 | 28292.87 | 2.15 |
| 5 | PUT|Stretch|Max0.65 | YES | 37 | 21365.74 | 1.64 | 23 | 19702.59 | 2.63 | 3506.67 | 37 | 27475.32 | 2.39 |
| 6 | PUT|Stretch|Max0.60 | YES | 34 | 7744.11 | 1.22 | 20 | 17508.7 | 2.67 | 2720.37 | 37 | 27428.38 | 2.39 |
| 7 | PUT|Stretch|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |
| 8 | PUT|Stretch|Min1.00 | no | 47 | 5435.53 | 1.12 | 31 | -8397.89 | 0.75 | 11289.61 | 49 | 17419.83 | 1.47 |

