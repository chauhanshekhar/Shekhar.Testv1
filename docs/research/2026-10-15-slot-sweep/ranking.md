# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 5, net 5910.44, PF 3.64, validation net 0 (PF NULL, 0 trades), test net 5910.44 (PF 3.64, 5 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 12, net 32849.38, PF 4.64, validation net 0 (PF NULL, 0 trades), test net 38781.8 (PF 13.59, 9 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL SENSEX

Baseline: trades 29, net 6749.41, PF 1.36, validation net -2355.75 (PF 0, 3 trades), test net 7923.02 (PF 1.49, 24 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

Baseline: trades 35, net 35909.41, PF 2.39, validation net -2355.75 (PF 0, 3 trades), test net 43015.43 (PF 3.45, 27 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|Slots|NoHandoff** - validation net 6814.27, PF 1.22; held-out test net 25183.38, PF 2.02 on 32 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Slots|NoHandoff | YES | 54 | 5333.81 | 1.09 | 36 | 6814.27 | 1.22 | 7056.53 | 32 | 25183.38 | 2.02 |
| 2 | PUT|Slots|Global2+NoHandoff | YES | 54 | 5333.81 | 1.09 | 36 | 6814.27 | 1.22 | 7056.53 | 32 | 25183.38 | 2.02 |
| 3 | PUT|Slots|PerSymbol1+NoHandoff | YES | 54 | 5333.81 | 1.09 | 36 | 6814.27 | 1.22 | 7056.53 | 32 | 25183.38 | 2.02 |
| 4 | PUT|Slots|Unlimited | YES | 54 | 5333.81 | 1.09 | 36 | 6814.27 | 1.22 | 7056.53 | 32 | 25183.38 | 2.02 |
| 5 | PUT|Slots|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 6 | PUT|Slots|Global2 | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 7 | PUT|Slots|Global3 | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 8 | PUT|Slots|PerSymbol1 | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 9 | PUT|Slots|PerSymbol2 | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|Slots|Global2+NoHandoff** - validation net 3361.66, PF 1.09; held-out test net 52340.17, PF 1.72 on 99 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Slots|Global2+NoHandoff | YES | 65 | 21637.04 | 1.37 | 42 | 3361.66 | 1.09 | 7848.76 | 99 | 52340.17 | 1.72 |
| 2 | PUT|Slots|PerSymbol1+NoHandoff | YES | 65 | 21637.04 | 1.37 | 42 | 3361.66 | 1.09 | 7848.76 | 101 | 58819.92 | 1.81 |
| 3 | PUT|Slots|Unlimited | YES | 65 | 21637.04 | 1.37 | 42 | 3361.66 | 1.09 | 7848.76 | 101 | 58819.92 | 1.81 |
| 4 | PUT|Slots|NoHandoff | YES | 62 | 7935.41 | 1.14 | 40 | 1572.62 | 1.04 | 7755.53 | 76 | 58331.42 | 2.29 |
| 5 | PUT|Slots|Global2 | no | 60 | 14005.61 | 1.24 | 37 | -6143.51 | 0.83 | 9736.92 | 80 | 42972.63 | 1.71 |
| 6 | PUT|Slots|Global3 | no | 60 | 14005.61 | 1.24 | 37 | -6143.51 | 0.83 | 9736.92 | 83 | 28041.67 | 1.4 |
| 7 | PUT|Slots|PerSymbol1 | no | 60 | 14005.61 | 1.24 | 37 | -6143.51 | 0.83 | 9736.92 | 83 | 28041.67 | 1.4 |
| 8 | PUT|Slots|PerSymbol2 | no | 60 | 14005.61 | 1.24 | 37 | -6143.51 | 0.83 | 9736.92 | 83 | 28041.67 | 1.4 |
| 9 | PUT|Slots|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |

