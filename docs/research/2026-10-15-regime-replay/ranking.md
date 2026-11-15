# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 1, net 1.3, PF NULL, validation net 0 (PF NULL, 0 trades), test net 1.3 (PF NULL, 1 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 100 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 2, net -2982.45, PF 0, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 0 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 100 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoPcr | no | 48 | 10603.49 | 1.26 | 28 | -1177.96 | 0.95 | 11173.97 | 12 | 4794.54 | 1.34 |

## CALL SENSEX

Baseline: trades 12, net 2303.6, PF 1.26, validation net 0 (PF NULL, 0 trades), test net 3462.42 (PF 1.44, 11 trades).
**Best validated: CALL|NoPcr+NoAtr** - validation net 1912.08, PF 1.11; held-out test net 3188.17, PF 1.28 on 19 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | YES | 34 | 226.28 | 1.01 | 26 | 1912.08 | 1.11 | 10274.23 | 19 | 3188.17 | 1.28 |

## CALL ALL

Baseline: trades 14, net -678.85, PF 0.94, validation net 0 (PF NULL, 0 trades), test net 3462.42 (PF 1.44, 11 trades).
**Best validated: CALL|NoEmaGap+NoPcr** - validation net 9084.88, PF 1.29; held-out test net 7736.96, PF 1.36 on 22 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoPcr | YES | 59 | 11068.03 | 1.24 | 38 | 9084.88 | 1.29 | 10775.47 | 22 | 7736.96 | 1.36 |
| 2 | CALL|NoPcr | no | 28 | -2366.68 | 0.9 | 18 | 7540.78 | 1.51 | 4995.38 | 14 | 1728.12 | 1.18 |
| 3 | CALL|NoRsi+NoPcr | no | 32 | -330.63 | 0.99 | 18 | 7540.78 | 1.51 | 4995.38 | 14 | 358.13 | 1.03 |
| 4 | CALL|NoPcr+NoAtr | no | 39 | -1303.64 | 0.95 | 30 | 3062.76 | 1.13 | 9531.2 | 19 | 3188.17 | 1.28 |

## PUT NIFTY

Baseline: trades 12, net -6627.55, PF 0.63, validation net -6904.12 (PF 0, 4 trades), test net -6525 (PF 0.42, 6 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 94 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 26418.78, PF 1.25, validation net 6190.87 (PF 1.25, 32 trades), test net 15075.24 (PF 1.82, 21 trades).
**Best validated: PUT|Rsi=25-42+Pcr=0.6** - validation net 23292.79, PF 2.09; held-out test net 20191.96, PF 1.92 on 25 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Rsi=25-42+Pcr=0.6 | YES | 67 | 8530.33 | 1.12 | 37 | 23292.79 | 2.09 | 4495.94 | 25 | 20191.96 | 1.92 |
| 2 | PUT|Rsi=25-42+Pcr=0.7 | YES | 67 | 8530.33 | 1.12 | 37 | 23292.79 | 2.09 | 4495.94 | 25 | 20191.96 | 1.92 |
| 3 | PUT|Rsi=25-42+Pcr=0.8 | YES | 67 | 8530.33 | 1.12 | 37 | 23292.79 | 2.09 | 4495.94 | 25 | 20191.96 | 1.92 |
| 4 | PUT|Rsi=25-42+Pcr=0.9 | YES | 67 | 8530.33 | 1.12 | 37 | 23292.79 | 2.09 | 4495.94 | 25 | 20191.96 | 1.92 |
| 5 | PUT|EmaGap=20 | YES | 36 | 15245.54 | 1.42 | 16 | 18096.63 | 3.85 | 3992.24 | 16 | 4656.57 | 1.25 |
| 6 | PUT|NoRsi | YES | 77 | 19501.2 | 1.25 | 41 | 17395.59 | 1.62 | 9327.4 | 29 | 9412.38 | 1.3 |
| 7 | PUT|NoRsi+NoPcr | YES | 77 | 19501.2 | 1.25 | 41 | 17395.59 | 1.62 | 9327.4 | 29 | 9412.38 | 1.3 |
| 8 | PUT|NoRsi+NoAtr | YES | 77 | 19501.2 | 1.25 | 41 | 17395.59 | 1.62 | 9327.4 | 29 | 9412.38 | 1.3 |
| 9 | PUT|RsiCeiling=42 | YES | 60 | 8648.78 | 1.13 | 35 | 15932.3 | 1.75 | 4495.94 | 23 | 16035.16 | 1.78 |
| 10 | PUT|Rsi=28-42+Pcr=0.6 | YES | 60 | 8648.78 | 1.13 | 35 | 15932.3 | 1.75 | 4495.94 | 23 | 16035.16 | 1.78 |

## PUT SENSEX

Baseline: trades 56, net 21872.06, PF 1.63, validation net 3451.5 (PF NULL, 2 trades), test net 14096.26 (PF 1.44, 47 trades).
**Best validated: PUT|NoPcr+NoAtr** - validation net 16731.37, PF 1.82; held-out test net 6263.57, PF 1.14 on 69 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+NoAtr | YES | 98 | 8535.25 | 1.15 | 53 | 16731.37 | 1.82 | 3498.87 | 69 | 6263.57 | 1.14 |
| 2 | PUT|NoPcr | YES | 45 | 3214.72 | 1.1 | 28 | 6487.16 | 1.68 | 4858.88 | 50 | 16834.04 | 1.53 |
| 3 | PUT|NoEmaGap+NoPcr | YES | 45 | 3214.72 | 1.1 | 28 | 6487.16 | 1.68 | 4858.88 | 50 | 16834.04 | 1.53 |
| 4 | PUT|NoRsi+NoPcr | no | 89 | -4529.5 | 0.94 | 50 | 8599.22 | 1.34 | 9405.65 | 88 | 47193.9 | 1.96 |

## PUT ALL

Baseline: trades 147, net 24843.19, PF 1.18, validation net 1522.76 (PF 1.05, 35 trades), test net 20012.9 (PF 1.49, 53 trades).
**Best validated: PUT|Rsi=25-42+Pcr=0.7** - validation net 16237.46, PF 1.57; held-out test net 25917.31, PF 1.57 on 65 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Rsi=25-42+Pcr=0.7 | YES | 72 | 9964.91 | 1.14 | 41 | 16237.46 | 1.57 | 6586.77 | 65 | 25917.31 | 1.57 |
| 2 | PUT|Rsi=25-42+Pcr=0.8 | YES | 71 | 9964.91 | 1.14 | 41 | 16237.46 | 1.57 | 6586.77 | 63 | 19830.16 | 1.41 |
| 3 | PUT|Rsi=25-42+Pcr=0.9 | YES | 71 | 9964.91 | 1.14 | 41 | 16237.46 | 1.57 | 6586.77 | 58 | 18684.33 | 1.42 |
| 4 | PUT|NoRsi+NoPcr | YES | 123 | 2093.76 | 1.02 | 71 | 16034.42 | 1.37 | 8868.24 | 96 | 41158.27 | 1.66 |
| 5 | PUT|NoPcr+NoAtr | YES | 117 | 16707.47 | 1.18 | 66 | 15807.39 | 1.46 | 6065.95 | 73 | 12300.83 | 1.23 |
| 6 | PUT|EmaGap=20 | YES | 39 | 15764.46 | 1.44 | 18 | 13395.01 | 2.21 | 6457.86 | 52 | 16595.88 | 1.41 |
| 7 | PUT|Rsi=25-35+Pcr=0.7 | YES | 64 | 10663.23 | 1.18 | 32 | 12741.08 | 1.52 | 7842.77 | 51 | 17934.63 | 1.47 |
| 8 | PUT|Rsi=25-35+Pcr=0.8 | YES | 63 | 10663.23 | 1.18 | 32 | 12741.08 | 1.52 | 7842.77 | 49 | 3527.01 | 1.08 |
| 9 | PUT|Rsi=25-35+Pcr=0.9 | YES | 63 | 10663.23 | 1.18 | 32 | 12741.08 | 1.52 | 7842.77 | 43 | 5552.31 | 1.15 |
| 10 | PUT|Rsi=25-35+Pcr=0.6 | YES | 64 | 10663.23 | 1.18 | 33 | 11475.58 | 1.44 | 7842.77 | 51 | 17934.63 | 1.47 |

