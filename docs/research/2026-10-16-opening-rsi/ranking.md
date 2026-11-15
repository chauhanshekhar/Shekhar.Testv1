# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 5, net 5910.44, PF 3.64, validation net 0 (PF NULL, 0 trades), test net 5910.44 (PF 3.64, 5 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 12, net 32849.38, PF 4.64, validation net 0 (PF NULL, 0 trades), test net 38781.8 (PF 13.59, 9 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL SENSEX

Baseline: trades 29, net 6749.41, PF 1.36, validation net -2355.75 (PF 0, 3 trades), test net 7923.02 (PF 1.49, 24 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

Baseline: trades 35, net 35909.41, PF 2.39, validation net -2355.75 (PF 0, 3 trades), test net 43015.43 (PF 3.45, 27 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|OpenRsi|GapUp45-60** - validation net 641.01, PF 1.03; held-out test net 23441.82, PF 2.85 on 20 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|OpenRsi|GapUp45-60 | YES | 50 | 13819.88 | 1.29 | 26 | 641.01 | 1.03 | 5486.57 | 20 | 23441.82 | 2.85 |
| 2 | PUT|OpenRsi|GapUp45-100 | YES | 50 | 13819.88 | 1.29 | 26 | 641.01 | 1.03 | 5486.57 | 20 | 23441.82 | 2.85 |
| 3 | PUT|OpenRsi|GapUp40-60 | YES | 53 | 14671.54 | 1.29 | 26 | 641.01 | 1.03 | 5486.57 | 25 | 43408.18 | 4.42 |
| 4 | PUT|OpenRsi|GapUp50-70 | YES | 47 | 16326.37 | 1.38 | 25 | 640.51 | 1.03 | 5486.57 | 19 | 18756.87 | 2.48 |
| 5 | PUT|OpenRsi|Baseline | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 5 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|OpenRsi|GapUp45-60 | no | 54 | 17031.12 | 1.37 | 29 | -2260.45 | 0.92 | 7374.12 | 56 | 12576.28 | 1.28 |
| 2 | PUT|OpenRsi|GapUp45-100 | no | 54 | 17031.12 | 1.37 | 29 | -2260.45 | 0.92 | 7374.12 | 56 | 14291.36 | 1.32 |
| 3 | PUT|OpenRsi|GapUp50-70 | no | 51 | 19537.62 | 1.47 | 28 | -2260.96 | 0.92 | 7374.12 | 49 | 9988.78 | 1.24 |
| 4 | PUT|OpenRsi|GapUp40-60 | no | 58 | 17882.78 | 1.37 | 30 | -4758.65 | 0.85 | 7374.12 | 64 | 17255.23 | 1.35 |
| 5 | PUT|OpenRsi|Baseline | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |

