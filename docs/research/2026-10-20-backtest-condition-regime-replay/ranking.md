# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 1, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 1 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 19, net 1241.59, PF 1.05, validation net 0 (PF NULL, 0 trades), test net 5989.88 (PF 1.42, 11 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoPcr | no | 54 | 29295.42 | 1.74 | 30 | -3362.48 | 0.88 | 12526.84 | 13 | 7924.4 | 1.55 |
| 2 | CALL|NoPcr+NoAtr | no | 40 | 4903.02 | 1.13 | 24 | -3618.92 | 0.84 | 8419.27 | 12 | 5782.74 | 1.4 |
| 3 | CALL|NoPcr | no | 40 | 4903.02 | 1.13 | 24 | -3721.65 | 0.83 | 8522 | 12 | 5782.74 | 1.4 |
| 4 | CALL|NoRsi+NoPcr | no | 43 | 2620.44 | 1.06 | 24 | -3721.65 | 0.83 | 8522 | 13 | 8316.08 | 1.58 |

## CALL SENSEX

Baseline: trades 13, net 5957.71, PF 1.66, validation net 0 (PF NULL, 0 trades), test net 7115.57 (PF 1.91, 12 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr | no | 24 | -1485.21 | 0.92 | 16 | 11368.21 | 2.47 | 2587.38 | 15 | 4175.17 | 1.39 |
| 2 | CALL|NoRsi+NoPcr | no | 27 | -2214.68 | 0.89 | 16 | 11368.21 | 2.47 | 2587.38 | 15 | 2806.32 | 1.23 |
| 3 | CALL|NoEmaGap+NoPcr | no | 24 | -1485.21 | 0.92 | 16 | 11368.21 | 2.47 | 2587.38 | 15 | 4175.17 | 1.39 |
| 4 | CALL|NoPcr+NoAtr | no | 40 | -2852.06 | 0.89 | 27 | 8215.11 | 1.55 | 4462.56 | 19 | 4359.95 | 1.34 |

## CALL ALL

Baseline: trades 31, net 7761.95, PF 1.25, validation net 0 (PF NULL, 0 trades), test net 13668.1 (PF 1.62, 22 trades).
**Best validated: CALL|NoPcr+NoAtr** - validation net 8170.83, PF 1.27; held-out test net 13983.78, PF 1.59 on 29 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | YES | 63 | 3983.11 | 1.08 | 43 | 8170.83 | 1.27 | 8662.22 | 29 | 13983.78 | 1.59 |
| 2 | CALL|NoPcr | YES | 53 | 2603.82 | 1.06 | 37 | 7777.54 | 1.29 | 7682.86 | 25 | 13799 | 1.63 |
| 3 | CALL|NoRsi+NoPcr | YES | 57 | 2155.72 | 1.04 | 37 | 7777.54 | 1.29 | 7682.86 | 26 | 14963.49 | 1.64 |
| 4 | CALL|NoEmaGap+NoPcr | YES | 66 | 29093.18 | 1.66 | 42 | 4550.85 | 1.13 | 12105.85 | 26 | 16918.38 | 1.77 |

## PUT NIFTY

Baseline: trades 15, net 8150.08, PF 1.52, validation net -6854.41 (PF 0, 4 trades), test net 8299.15 (PF 1.94, 9 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 694 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 175, net 58752.73, PF 1.39, validation net 9392.31 (PF 1.34, 37 trades), test net 11781.56 (PF 1.33, 37 trades).
**Best validated: PUT|Rsi=25-42+Pcr=0.6** - validation net 27220.97, PF 2.06; held-out test net 28168.92, PF 1.61 on 52 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Rsi=25-42+Pcr=0.6 | YES | 127 | 18237.46 | 1.15 | 45 | 27220.97 | 2.06 | 6677.95 | 52 | 28168.92 | 1.61 |
| 2 | PUT|Rsi=25-42+Pcr=0.7 | YES | 127 | 18237.46 | 1.15 | 45 | 27220.97 | 2.06 | 6677.95 | 52 | 28168.92 | 1.61 |
| 3 | PUT|Rsi=25-42+Pcr=0.8 | YES | 127 | 18237.46 | 1.15 | 45 | 27220.97 | 2.06 | 6677.95 | 52 | 28168.92 | 1.61 |
| 4 | PUT|Rsi=25-42+Pcr=0.9 | YES | 127 | 18237.46 | 1.15 | 45 | 27220.97 | 2.06 | 6677.95 | 52 | 28168.92 | 1.61 |
| 5 | PUT|NoRsi+NoAtr | YES | 177 | 26246.76 | 1.16 | 62 | 25681.27 | 1.64 | 7326.17 | 72 | 7348.88 | 1.09 |
| 6 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.6|Atr=0.02 | YES | 135 | 21765.46 | 1.17 | 51 | 25481 | 1.82 | 6657.51 | 51 | 33506.76 | 1.78 |
| 7 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.7|Atr=0.02 | YES | 135 | 21765.46 | 1.17 | 51 | 25481 | 1.82 | 6657.51 | 51 | 33506.76 | 1.78 |
| 8 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.8|Atr=0.02 | YES | 135 | 21765.46 | 1.17 | 51 | 25481 | 1.82 | 6657.51 | 51 | 33506.76 | 1.78 |
| 9 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.9|Atr=0.02 | YES | 135 | 21765.46 | 1.17 | 51 | 25481 | 1.82 | 6657.51 | 51 | 33506.76 | 1.78 |
| 10 | PUT|NoRsi | YES | 171 | 21666.69 | 1.13 | 59 | 22706.5 | 1.59 | 8446.54 | 72 | 7348.88 | 1.09 |

## PUT SENSEX

Baseline: trades 85, net 35419.21, PF 1.68, validation net -1111.6 (PF 0, 1 trades), test net 27800.94 (PF 1.56, 75 trades).
**Best validated: PUT|NoPcr+NoAtr** - validation net 18575.03, PF 1.61; held-out test net 43574.4, PF 1.81 on 91 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+NoAtr | YES | 127 | 36619.33 | 1.61 | 74 | 18575.03 | 1.61 | 5997.99 | 91 | 43574.4 | 1.81 |
| 2 | PUT|NoRsi+NoPcr | YES | 124 | 6311.53 | 1.07 | 55 | 12519.31 | 1.5 | 4753.55 | 131 | 52473.43 | 1.62 |
| 3 | PUT|NoPcr | YES | 57 | 17491.14 | 1.58 | 33 | 2030.7 | 1.14 | 5985.45 | 78 | 30698.86 | 1.58 |
| 4 | PUT|NoEmaGap+NoPcr | YES | 57 | 17491.14 | 1.58 | 33 | 2030.7 | 1.14 | 5985.45 | 78 | 30698.86 | 1.58 |

## PUT ALL

Baseline: trades 238, net 79261.9, PF 1.42, validation net 3625.16 (PF 1.11, 41 trades), test net 39283.5 (PF 1.61, 91 trades).
**Best validated: PUT|Rsi=25-42+Pcr=0.9** - validation net 22565.41, PF 1.74; held-out test net 50551.71, PF 1.66 on 105 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Rsi=25-42+Pcr=0.9 | YES | 133 | 16058.11 | 1.12 | 48 | 22565.41 | 1.74 | 6677.95 | 105 | 50551.71 | 1.66 |
| 2 | PUT|NoRsi+NoPcr | YES | 219 | 9648.75 | 1.05 | 93 | 22190.14 | 1.38 | 13083.56 | 152 | 51209.96 | 1.42 |
| 3 | PUT|Rsi=25-42+Pcr=0.7 | YES | 135 | 17415.91 | 1.14 | 49 | 21453.81 | 1.68 | 6677.95 | 109 | 56057.98 | 1.74 |
| 4 | PUT|Rsi=25-42+Pcr=0.8 | YES | 134 | 17415.91 | 1.14 | 49 | 21453.81 | 1.68 | 6677.95 | 106 | 54645.3 | 1.71 |
| 5 | PUT|NoRsi+NoAtr | YES | 198 | 27669.89 | 1.16 | 68 | 20687.33 | 1.45 | 7326.17 | 170 | 49003.47 | 1.4 |
| 6 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.6|Atr=0.02 | YES | 158 | 28525.43 | 1.21 | 59 | 20586.68 | 1.53 | 6657.51 | 125 | 66176.13 | 1.84 |
| 7 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.9|Atr=0.02 | YES | 149 | 24825.69 | 1.19 | 57 | 20527.9 | 1.54 | 6657.51 | 117 | 63642.75 | 1.86 |
| 8 | PUT|Rsi=25-42+Pcr=0.6 | YES | 137 | 14666.75 | 1.11 | 50 | 20298.88 | 1.62 | 6677.95 | 109 | 55892.44 | 1.71 |
| 9 | PUT|Grid|Rsi=25-35|Gap=5|Pcr=0.6|Atr=0.02 | YES | 119 | 46773.54 | 1.51 | 43 | 19759.92 | 1.68 | 6053.86 | 98 | 59359.22 | 1.98 |
| 10 | PUT|NoPcr+NoAtr | YES | 174 | 32079.74 | 1.25 | 94 | 19745.14 | 1.4 | 7515.08 | 103 | 57180.47 | 1.88 |

