# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 5, net 5910.44, PF 3.64, validation net 0 (PF NULL, 0 trades), test net 5910.44 (PF 3.64, 5 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 12, net 32849.38, PF 4.64, validation net 0 (PF NULL, 0 trades), test net 38781.8 (PF 13.59, 9 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL SENSEX

Baseline: trades 29, net 6749.41, PF 1.36, validation net -2355.75 (PF 0, 3 trades), test net 7923.02 (PF 1.49, 24 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

Baseline: trades 35, net 35909.41, PF 2.39, validation net -2355.75 (PF 0, 3 trades), test net 43015.43 (PF 3.45, 27 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|Stretch|Max0.75** - validation net 21251.32, PF 2.75; held-out test net 18695.18, PF 2.49 on 18 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Stretch|Max0.75 | YES | 38 | 9947.74 | 1.27 | 21 | 21251.32 | 2.75 | 2755 | 18 | 18695.18 | 2.49 |
| 2 | PUT|Stretch|Max1.00 | YES | 38 | 10618.61 | 1.31 | 24 | 20111.83 | 2.45 | 3367.31 | 20 | 20922.36 | 2.4 |
| 3 | PUT|Stretch|Max1.50 | YES | 41 | 10181.09 | 1.29 | 28 | 9301.43 | 1.42 | 7173.27 | 22 | 7224.66 | 1.35 |
| 4 | PUT|Stretch|Max1.25 | no | 42 | -5.12 | 1 | 27 | 9152.36 | 1.47 | 5186.03 | 22 | 6657.4 | 1.32 |
| 5 | PUT|Stretch|Max0.50 | no | 28 | -48.54 | 1 | 16 | 4205.04 | 1.32 | 3617.29 | 13 | 14763.23 | 2.31 |
| 6 | PUT|Stretch|Max2.00 | no | 43 | 24445.99 | 1.64 | 29 | -588.67 | 0.98 | 8962.07 | 22 | 10844.74 | 1.61 |
| 7 | PUT|Stretch|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 8 | PUT|Stretch|Min1.50 | no | 38 | 12878.78 | 1.35 | 24 | -7065.66 | 0.72 | 12515.55 | 20 | 16167.91 | 1.79 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 8 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|Stretch|Max0.75** - validation net 22594.27, PF 2.65; held-out test net 25881.92, PF 2.18 on 39 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Stretch|Max0.75 | YES | 39 | 15774.26 | 1.43 | 25 | 22594.27 | 2.65 | 3452.91 | 39 | 25881.92 | 2.18 |
| 2 | PUT|Stretch|Max1.00 | YES | 39 | 14162.72 | 1.42 | 28 | 21641.48 | 2.44 | 3495.89 | 42 | 26655.39 | 2.04 |
| 3 | PUT|Stretch|Max1.25 | YES | 44 | 3522.25 | 1.08 | 30 | 15356.47 | 1.85 | 5186.03 | 45 | 18551.09 | 1.62 |
| 4 | PUT|Stretch|Max1.50 | YES | 43 | 19485.51 | 1.59 | 31 | 14380.33 | 1.73 | 7173.27 | 48 | 4242.24 | 1.11 |
| 5 | PUT|Stretch|Max2.00 | YES | 46 | 24893.52 | 1.69 | 32 | 6285.44 | 1.28 | 7518.67 | 52 | 15010.11 | 1.38 |
| 6 | PUT|Stretch|Max0.50 | YES | 29 | 10709.8 | 1.38 | 18 | 5979.13 | 1.46 | 3617.29 | 36 | 21074.35 | 1.93 |
| 7 | PUT|Stretch|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |
| 8 | PUT|Stretch|Min1.50 | no | 41 | 15255.71 | 1.42 | 27 | -12723.33 | 0.6 | 14948.96 | 45 | 15455.94 | 1.44 |

