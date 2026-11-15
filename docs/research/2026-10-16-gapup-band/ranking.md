# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 7 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|OpenBand|BlockOnGapUp** - validation net 401.04, PF 1.02; held-out test net 32947.4, PF 3.23 on 25 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|OpenBand|BlockOnGapUp | YES | 46 | 14816.42 | 1.34 | 23 | 401.04 | 1.02 | 7056.53 | 25 | 32947.4 | 3.23 |
| 2 | PUT|OpenBand|GapUp45-60 | no | 49 | 16588.02 | 1.37 | 25 | -1389.09 | 0.94 | 7056.53 | 29 | 36391.02 | 2.85 |
| 3 | PUT|OpenBand|GapUp45-100 | no | 49 | 16588.02 | 1.37 | 25 | -1389.09 | 0.94 | 7056.53 | 29 | 36391.02 | 2.85 |
| 4 | PUT|OpenBand|GapUp50-60 | no | 47 | 16326.37 | 1.38 | 24 | -1389.6 | 0.94 | 7056.53 | 27 | 33624.65 | 2.9 |
| 5 | PUT|OpenBand|GapUp50-100 | no | 47 | 16326.37 | 1.38 | 24 | -1389.6 | 0.94 | 7056.53 | 27 | 33624.65 | 2.9 |
| 6 | PUT|OpenBand|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |
| 7 | PUT|OpenBand|GapUp30-35 | no | 49 | 5392.03 | 1.1 | 31 | -7719.52 | 0.76 | 12941.93 | 26 | 29902.31 | 2.68 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 7 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 7 of 7 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|OpenBand|BlockOnGapUp | no | 50 | 15525.92 | 1.35 | 27 | -618.66 | 0.98 | 7374.12 | 51 | 22156.88 | 1.56 |
| 2 | PUT|OpenBand|GapUp45-60 | no | 54 | 17563.27 | 1.39 | 29 | -2408.79 | 0.92 | 7929.48 | 60 | 23407.06 | 1.5 |
| 3 | PUT|OpenBand|GapUp45-100 | no | 54 | 17563.27 | 1.39 | 29 | -2408.79 | 0.92 | 7929.48 | 60 | 26017.43 | 1.56 |
| 4 | PUT|OpenBand|GapUp50-60 | no | 52 | 17301.62 | 1.39 | 28 | -2409.29 | 0.92 | 7929.48 | 54 | 18656.43 | 1.43 |
| 5 | PUT|OpenBand|GapUp50-100 | no | 52 | 17301.62 | 1.39 | 28 | -2409.29 | 0.92 | 7929.48 | 54 | 21266.81 | 1.49 |
| 6 | PUT|OpenBand|GapUp30-35 | no | 52 | 10276.71 | 1.21 | 34 | -7189.41 | 0.8 | 12941.93 | 54 | 19047.47 | 1.42 |
| 7 | PUT|OpenBand|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |

