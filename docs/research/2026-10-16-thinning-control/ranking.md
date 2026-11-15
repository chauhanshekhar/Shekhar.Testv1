# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|Thin|MaxPerDay2** - validation net 2705.37, PF 1.15; held-out test net 20952.09, PF 3.47 on 16 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Thin|MaxPerDay2 | YES | 32 | 3268.26 | 1.09 | 22 | 2705.37 | 1.15 | 9111.1 | 16 | 20952.09 | 3.47 |
| 2 | PUT|Thin|KeepEvery4 | YES | 39 | 10227.96 | 1.27 | 26 | 2030.79 | 1.09 | 8362.49 | 24 | 14211.33 | 1.68 |
| 3 | PUT|Thin|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 4 | PUT|Thin|KeepEvery2 | no | 48 | 11689.82 | 1.23 | 31 | -4686.84 | 0.86 | 14551.45 | 26 | 16829.82 | 1.61 |
| 5 | PUT|Thin|KeepEvery3 | no | 45 | 1517.23 | 1.03 | 28 | -9149.34 | 0.69 | 16487.59 | 26 | 201.68 | 1.01 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|Thin|KeepEvery4** - validation net 2225.92, PF 1.09; held-out test net 31950.59, PF 1.91 on 48 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Thin|KeepEvery4 | YES | 42 | 4216.31 | 1.1 | 29 | 2225.92 | 1.09 | 9287.1 | 48 | 31950.59 | 1.91 |
| 2 | PUT|Thin|KeepEvery2 | no | 50 | 12514.5 | 1.26 | 33 | -1443.44 | 0.96 | 11985.97 | 55 | 16537.41 | 1.37 |
| 3 | PUT|Thin|MaxPerDay2 | no | 38 | 7624.08 | 1.2 | 26 | -2629.51 | 0.9 | 9109.8 | 31 | 11621.43 | 1.51 |
| 4 | PUT|Thin|MaxPerDay1 | no | 25 | 9003.63 | 1.4 | 17 | -6324.79 | 0.67 | 12226.01 | 19 | 48.11 | 1 |
| 5 | PUT|Thin|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |
| 6 | PUT|Thin|KeepEvery3 | no | 50 | -19574.57 | 0.66 | 32 | -12010.25 | 0.64 | 20897.73 | 49 | 6512.67 | 1.15 |

