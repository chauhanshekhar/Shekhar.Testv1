# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 11 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|ST|Normal** - validation net 5811.2, PF 1.31; held-out test net 875.02, PF 1.07 on 15 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|ST|Normal | YES | 44 | 1497.23 | 1.03 | 22 | 5811.2 | 1.31 | 9095.15 | 15 | 875.02 | 1.07 |
| 2 | PUT|ST|Normal+AtExtreme | YES | 39 | 4400.35 | 1.1 | 21 | 4478.8 | 1.24 | 10427.55 | 13 | -6345.5 | 0.65 |
| 3 | PUT|ST|KeepEvery4 | YES | 39 | 10227.96 | 1.27 | 26 | 2030.79 | 1.09 | 8362.49 | 24 | 14211.33 | 1.68 |
| 4 | PUT|ST|GapUp+AtExtreme | no | 47 | 13465.02 | 1.3 | 29 | -575.58 | 0.98 | 7056.53 | 26 | 30340.23 | 2.75 |
| 5 | PUT|ST|GapUp | no | 47 | 13465.02 | 1.3 | 30 | -983.73 | 0.96 | 7056.53 | 27 | 27295.13 | 2.34 |
| 6 | PUT|ST|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 7 | PUT|ST|KeepEvery2 | no | 48 | 11689.82 | 1.23 | 31 | -4686.84 | 0.86 | 14551.45 | 26 | 16829.82 | 1.61 |
| 8 | PUT|ST|GapDown+AtExtreme | no | 41 | -15388.86 | 0.69 | 22 | -12094.36 | 0.52 | 12236.77 | 26 | 39298.28 | 4.31 |
| 9 | PUT|ST|GapDown | no | 41 | -5960.17 | 0.88 | 24 | -16383.26 | 0.44 | 16525.67 | 26 | 39298.28 | 4.31 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 11 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|ST|KeepEvery4** - validation net 2225.92, PF 1.09; held-out test net 31950.59, PF 1.91 on 48 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|ST|KeepEvery4 | YES | 42 | 4216.31 | 1.1 | 29 | 2225.92 | 1.09 | 9287.1 | 48 | 31950.59 | 1.91 |
| 2 | PUT|ST|Normal | YES | 51 | 3987.05 | 1.07 | 25 | 475.02 | 1.02 | 9095.15 | 54 | 6822.59 | 1.15 |
| 3 | PUT|ST|KeepEvery2 | no | 50 | 12514.5 | 1.26 | 33 | -1443.44 | 0.96 | 11985.97 | 55 | 16537.41 | 1.37 |
| 4 | PUT|ST|Normal+AtExtreme | no | 44 | 5956.85 | 1.13 | 24 | -2664.13 | 0.89 | 10427.55 | 45 | 6458.25 | 1.18 |
| 5 | PUT|ST|GapUp+AtExtreme | no | 51 | 14174.52 | 1.31 | 33 | -5910.46 | 0.83 | 8539.8 | 56 | 15464.39 | 1.33 |
| 6 | PUT|ST|GapUp | no | 51 | 14174.52 | 1.31 | 34 | -6318.61 | 0.82 | 8947.94 | 57 | 15084.32 | 1.32 |
| 7 | PUT|ST|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |
| 8 | PUT|ST|Any | no | 33 | 8429.34 | 1.28 | 17 | -9273.67 | 0.56 | 12811.39 | 46 | -2767.56 | 0.93 |
| 9 | PUT|ST|GapDown+AtExtreme | no | 42 | -8767.4 | 0.81 | 26 | -15192.73 | 0.49 | 16904.38 | 49 | 48619.62 | 3.02 |
| 10 | PUT|ST|GapDown | no | 42 | 661.3 | 1.02 | 28 | -19481.64 | 0.43 | 21193.29 | 53 | 35538.76 | 2.11 |

