# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 17, net 15281.16, PF 1.96, validation net -6968.9 (PF 0, 4 trades), test net 12551.98 (PF 2.4, 10 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 94 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 192, net 81948.58, PF 1.52, validation net 14897.43 (PF 1.46, 43 trades), test net 33853.53 (PF 1.93, 43 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 36099.97, PF 1.44; held-out test net 48250.13, PF 1.42 on 122 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 292 | 58910.47 | 1.25 | 103 | 36099.97 | 1.44 | 20434.39 | 122 | 48250.13 | 1.42 |
| 2 | PUT|Rsi=25-42+Pcr=0.6 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 3 | PUT|Rsi=25-42+Pcr=0.7 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 4 | PUT|Rsi=25-42+Pcr=0.8 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 5 | PUT|Rsi=25-42+Pcr=0.9 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 6 | PUT|Rsi=25-40+Pcr=0.6 | YES | 127 | 22916.4 | 1.2 | 48 | 27011.82 | 1.86 | 8539.48 | 50 | 40100.69 | 1.94 |
| 7 | PUT|Rsi=25-40+Pcr=0.7 | YES | 127 | 22916.4 | 1.2 | 48 | 27011.82 | 1.86 | 8539.48 | 50 | 40100.69 | 1.94 |
| 8 | PUT|Rsi=25-40+Pcr=0.8 | YES | 127 | 22916.4 | 1.2 | 48 | 27011.82 | 1.86 | 8539.48 | 50 | 40100.69 | 1.94 |
| 9 | PUT|Rsi=25-40+Pcr=0.9 | YES | 127 | 22916.4 | 1.2 | 48 | 27011.82 | 1.86 | 8539.48 | 50 | 40100.69 | 1.94 |
| 10 | PUT|NoRsi+NoAtr | YES | 190 | 31177.65 | 1.19 | 69 | 23972.52 | 1.48 | 15082.31 | 87 | 32701.23 | 1.35 |

## PUT SENSEX

Baseline: trades 95, net 59153.35, PF 2.2, validation net -1111.6 (PF 0, 1 trades), test net 47716.84 (PF 1.99, 83 trades).
**Best validated: PUT|NoPcr+NoAtr** - validation net 14695.74, PF 1.33; held-out test net 59778.8, PF 2.06 on 104 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+NoAtr | YES | 152 | 40075.38 | 1.58 | 96 | 14695.74 | 1.33 | 9353.15 | 104 | 59778.8 | 2.06 |
| 2 | PUT|NoRsi+NoPcr | YES | 154 | 22816.73 | 1.25 | 73 | 10752.28 | 1.27 | 9181.34 | 152 | 68538.47 | 1.67 |
| 3 | PUT|NoEmaGap+NoPcr | YES | 68 | 12746.01 | 1.32 | 48 | 3262.88 | 1.13 | 12646.91 | 90 | 54015.88 | 2.01 |
| 4 | PUT|NoPcr | no | 61 | 21968.27 | 1.75 | 45 | -1532.31 | 0.94 | 12286.07 | 87 | 48311.97 | 1.91 |

## PUT ALL

Baseline: trades 258, net 116932.84, PF 1.62, validation net 6816.93 (PF 1.17, 47 trades), test net 66015.48 (PF 2.02, 98 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 29286.67, PF 1.32; held-out test net 61181.74, PF 1.36 on 202 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 300 | 55734.07 | 1.24 | 111 | 29286.67 | 1.32 | 22670.39 | 202 | 61181.74 | 1.36 |
| 2 | PUT|Rsi=25-42+Pcr=0.9 | YES | 142 | 16450.84 | 1.13 | 52 | 24548.36 | 1.69 | 8710.41 | 115 | 71961.3 | 1.8 |
| 3 | PUT|NoEmaGap+NoAtr | YES | 168 | 62932.85 | 1.59 | 75 | 24043.55 | 1.42 | 11395.77 | 125 | 79648.08 | 2.06 |
| 4 | PUT|Rsi=25-42+Pcr=0.7 | YES | 145 | 19277.42 | 1.15 | 53 | 23436.75 | 1.64 | 8710.41 | 117 | 77827.03 | 1.89 |
| 5 | PUT|Rsi=25-42+Pcr=0.8 | YES | 144 | 19277.42 | 1.15 | 53 | 23436.75 | 1.64 | 8710.41 | 114 | 76414.35 | 1.86 |
| 6 | PUT|Rsi=25-42+Pcr=0.6 | YES | 146 | 19277.42 | 1.15 | 54 | 22281.82 | 1.59 | 8710.41 | 117 | 77661.49 | 1.86 |
| 7 | PUT|Rsi=25-40+Pcr=0.9 | YES | 131 | 25493.17 | 1.22 | 51 | 20042.92 | 1.52 | 8710.41 | 108 | 72095.91 | 1.91 |
| 8 | PUT|NoEmaGap | YES | 137 | 53711.2 | 1.56 | 62 | 19391.1 | 1.37 | 8729.84 | 110 | 70570.21 | 1.99 |
| 9 | PUT|Rsi=32-42+Pcr=0.6 | YES | 126 | 22709.03 | 1.21 | 44 | 18966.91 | 1.67 | 6142.4 | 98 | 50854.79 | 1.66 |
| 10 | PUT|Rsi=32-42+Pcr=0.7 | YES | 125 | 22709.03 | 1.21 | 44 | 18966.91 | 1.67 | 6142.4 | 98 | 50854.79 | 1.66 |

