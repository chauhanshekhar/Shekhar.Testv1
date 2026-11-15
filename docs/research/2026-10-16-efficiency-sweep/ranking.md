# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 10 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|Eff|FlatCap0.85** - validation net 21992.69, PF 2.81; held-out test net 23284.71, PF 2.85 on 19 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Eff|FlatCap0.85 | YES | 38 | 9940.31 | 1.27 | 21 | 21992.69 | 2.81 | 2755 | 19 | 23284.71 | 2.85 |
| 2 | PUT|Eff|Below0.65+CapEarly | YES | 41 | 18969.4 | 1.48 | 23 | 17617.58 | 2.23 | 4163.99 | 23 | 26537.76 | 2.81 |
| 3 | PUT|Eff|Below0.65+FreeEarly | YES | 41 | 18969.4 | 1.48 | 23 | 17617.58 | 2.23 | 4163.99 | 23 | 26537.76 | 2.81 |
| 4 | PUT|Eff|Below0.50+CapEarly | YES | 44 | 22806.03 | 1.57 | 25 | 9947.56 | 1.51 | 6355.08 | 23 | 26922.38 | 2.86 |
| 5 | PUT|Eff|Below0.50+FreeEarly | YES | 44 | 22806.03 | 1.57 | 25 | 9947.56 | 1.51 | 6355.08 | 23 | 26922.38 | 2.86 |
| 6 | PUT|Eff|Below0.35+CapEarly | YES | 48 | 3885.49 | 1.07 | 25 | 8147.18 | 1.39 | 6439.84 | 24 | 27068.06 | 2.89 |
| 7 | PUT|Eff|Below0.35+FreeEarly | YES | 48 | 3885.49 | 1.07 | 25 | 8147.18 | 1.39 | 6439.84 | 24 | 27068.06 | 2.89 |
| 8 | PUT|Eff|Below0.25+CapEarly | YES | 50 | 10262.21 | 1.2 | 26 | 7754.96 | 1.37 | 5318.25 | 25 | 25822.78 | 2.75 |
| 9 | PUT|Eff|Below0.25+FreeEarly | YES | 50 | 10262.21 | 1.2 | 26 | 7754.96 | 1.37 | 5318.25 | 25 | 25822.78 | 2.75 |
| 10 | PUT|Eff|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 10 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|Eff|FlatCap0.85** - validation net 23333.51, PF 2.7; held-out test net 25747.65, PF 2.17 on 39 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Eff|FlatCap0.85 | YES | 39 | 15766.83 | 1.43 | 25 | 23333.51 | 2.7 | 3500.11 | 39 | 25747.65 | 2.17 |
| 2 | PUT|Eff|Below0.65+CapEarly | YES | 44 | 21748.28 | 1.59 | 26 | 16952.62 | 2.03 | 4163.99 | 52 | 19340.12 | 1.49 |
| 3 | PUT|Eff|Below0.65+FreeEarly | YES | 44 | 21748.28 | 1.59 | 26 | 16952.62 | 2.03 | 4163.99 | 52 | 19340.12 | 1.49 |
| 4 | PUT|Eff|Below0.50+CapEarly | YES | 45 | 27624.9 | 1.78 | 29 | 6849.18 | 1.28 | 6596.9 | 54 | 15741.8 | 1.36 |
| 5 | PUT|Eff|Below0.50+FreeEarly | YES | 45 | 27624.9 | 1.78 | 29 | 6849.18 | 1.28 | 6596.9 | 54 | 15741.8 | 1.36 |
| 6 | PUT|Eff|Below0.35+CapEarly | YES | 49 | 8704.35 | 1.18 | 29 | 2812.3 | 1.1 | 6596.9 | 54 | 27128.85 | 1.74 |
| 7 | PUT|Eff|Below0.35+FreeEarly | YES | 49 | 8704.35 | 1.18 | 29 | 2812.3 | 1.1 | 6596.9 | 54 | 27128.85 | 1.74 |
| 8 | PUT|Eff|Below0.25+CapEarly | YES | 52 | 997.67 | 1.02 | 30 | 2420.08 | 1.09 | 7209.67 | 56 | 24301.66 | 1.61 |
| 9 | PUT|Eff|Below0.25+FreeEarly | YES | 52 | 997.67 | 1.02 | 30 | 2420.08 | 1.09 | 7209.67 | 56 | 24301.66 | 1.61 |
| 10 | PUT|Eff|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |

