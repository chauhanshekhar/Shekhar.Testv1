# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 16, net 30891.65, PF 3.04, validation net 8610.58 (PF 2.7, 5 trades), test net 6315.54 (PF NULL, 2 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 100 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 79, net 41825.37, PF 1.51, validation net 28915.13 (PF 3.44, 16 trades), test net 9679.18 (PF 1.33, 25 trades).
**Best validated: CALL|NoNoTradeWindow** - validation net 36367.04, PF 3.94; held-out test net 13687.09, PF 1.44 on 28 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoNoTradeWindow | YES | 39 | 763.41 | 1.02 | 19 | 36367.04 | 3.94 | 3270.52 | 28 | 13687.09 | 1.44 |
| 2 | CALL|EmaGap=5 | YES | 39 | 4962.85 | 1.12 | 16 | 31520.38 | 3.66 | 3360.86 | 24 | 10825.26 | 1.4 |
| 3 | CALL|NoEmaGap | YES | 47 | 1251.56 | 1.03 | 21 | 30453.06 | 2.62 | 5131.73 | 29 | 7954.44 | 1.22 |
| 4 | CALL|NoEmaGap+NoPcr | YES | 47 | 1251.56 | 1.03 | 21 | 30453.06 | 2.62 | 5131.73 | 29 | 7954.44 | 1.22 |
| 5 | CALL|NoEmaGap+NoAtr | YES | 47 | 1251.56 | 1.03 | 21 | 30453.06 | 2.62 | 5131.73 | 29 | 7954.44 | 1.22 |
| 6 | CALL|Baseline | YES | 38 | 3231.06 | 1.08 | 16 | 28915.13 | 3.44 | 4590 | 25 | 9679.18 | 1.33 |
| 7 | CALL|NoPcr | YES | 38 | 3231.06 | 1.08 | 16 | 28915.13 | 3.44 | 4590 | 25 | 9679.18 | 1.33 |
| 8 | CALL|NoAtr | YES | 38 | 3231.06 | 1.08 | 16 | 28915.13 | 3.44 | 4590 | 25 | 9679.18 | 1.33 |
| 9 | CALL|NoEarlyTrendGate | YES | 38 | 3231.06 | 1.08 | 16 | 28915.13 | 3.44 | 4590 | 25 | 9679.18 | 1.33 |
| 10 | CALL|RsiFloor=40 | YES | 38 | 3231.06 | 1.08 | 16 | 28915.13 | 3.44 | 4590 | 25 | 9679.18 | 1.33 |

## CALL SENSEX

Baseline: trades 70, net 30047.19, PF 1.53, validation net 9518.22 (PF 1.96, 13 trades), test net 25961.92 (PF 3.46, 22 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 6 of 100 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoAtr | no | 47 | -11031.75 | 0.78 | 27 | 17758.67 | 2.07 | 5630.28 | 25 | 27672.01 | 3.11 |
| 2 | CALL|NoAtr | no | 38 | -3598.95 | 0.9 | 21 | 16512.35 | 2.24 | 5654.14 | 24 | 31014.44 | 3.96 |
| 3 | CALL|AtrMin=0.02 | no | 38 | -3598.95 | 0.9 | 21 | 16512.35 | 2.24 | 5654.14 | 24 | 31014.44 | 3.96 |
| 4 | CALL|NoRsi+NoAtr | no | 38 | -3598.95 | 0.9 | 21 | 16512.35 | 2.24 | 5654.14 | 24 | 31014.44 | 3.96 |
| 5 | CALL|NoPcr+NoAtr | no | 38 | -3598.95 | 0.9 | 21 | 16512.35 | 2.24 | 5654.14 | 24 | 31014.44 | 3.96 |
| 6 | CALL|NoNoTradeWindow | no | 35 | -5432.95 | 0.85 | 17 | 11811.12 | 1.93 | 5076.66 | 24 | 26201.07 | 3.2 |

## CALL ALL

Baseline: trades 99, net 36582.27, PF 1.34, validation net 13487.19 (PF 1.66, 20 trades), test net 18515.71 (PF 1.61, 29 trades).
**Best validated: CALL|AtrMin=0.04** - validation net 24492.03, PF 3.38; held-out test net 1023.95, PF 1.03 on 25 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|AtrMin=0.04 | YES | 47 | 10239.84 | 1.2 | 15 | 24492.03 | 3.38 | 5579.25 | 25 | 1023.95 | 1.03 |
| 2 | CALL|NoEmaGap+NoAtr | YES | 58 | 12806.89 | 1.2 | 27 | 21694.43 | 1.83 | 7997.93 | 28 | 30843.36 | 2.08 |
| 3 | CALL|NoNoTradeWindow | YES | 51 | 2111.71 | 1.04 | 23 | 20939.1 | 2 | 5446.7 | 32 | 20671.76 | 1.64 |
| 4 | CALL|NoAtr | YES | 51 | 9943.14 | 1.18 | 20 | 20290.12 | 2.08 | 6852.51 | 29 | 20540.34 | 1.72 |
| 5 | CALL|AtrMin=0.02 | YES | 51 | 9943.14 | 1.18 | 20 | 20290.12 | 2.08 | 6852.51 | 29 | 20540.34 | 1.72 |
| 6 | CALL|NoPcr+NoAtr | YES | 51 | 9943.14 | 1.18 | 20 | 20290.12 | 2.08 | 6852.51 | 29 | 20540.34 | 1.72 |
| 7 | CALL|NoRsi+NoAtr | YES | 52 | 9943.14 | 1.18 | 20 | 19988.1 | 2.05 | 6852.51 | 29 | 20540.34 | 1.72 |
| 8 | CALL|RsiCeiling=70+NoEarlyTrendGate | YES | 50 | 4762.73 | 1.08 | 16 | 18134.8 | 2.35 | 4277.52 | 28 | 22254.66 | 1.9 |
| 9 | CALL|RsiCeiling=70 | YES | 50 | 4762.73 | 1.08 | 16 | 18134.8 | 2.35 | 4277.52 | 28 | 22254.66 | 1.9 |
| 10 | CALL|Rsi=40-70+Pcr=0.7 | YES | 50 | 4762.73 | 1.08 | 16 | 18134.8 | 2.35 | 4277.52 | 28 | 22254.66 | 1.9 |

## PUT NIFTY

Baseline: trades 5, net -5566.27, PF 0.41, validation net 0 (PF NULL, 0 trades), test net -510.24 (PF 0.88, 3 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 94 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 35, net 4749.68, PF 1.09, validation net -426.61 (PF 0.97, 10 trades), test net 10107.19 (PF 2.06, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 94 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoEmaGap | no | 19 | -1480.23 | 0.95 | 18 | 7564.14 | 1.46 | 6887.01 | 7 | 10107.19 | 2.06 |
| 2 | PUT|NoEmaGap+NoPcr | no | 19 | -1480.23 | 0.95 | 18 | 7564.14 | 1.46 | 6887.01 | 7 | 10107.19 | 2.06 |
| 3 | PUT|NoEmaGap+NoAtr | no | 19 | -1480.23 | 0.95 | 18 | 7564.14 | 1.46 | 6887.01 | 7 | 10107.19 | 2.06 |
| 4 | PUT|NoRsi+NoEmaGap | no | 41 | -4244.07 | 0.92 | 20 | 6034.46 | 1.32 | 6887.01 | 18 | -3624.4 | 0.88 |

## PUT SENSEX

Baseline: trades 1, net -3249.87, PF 0, validation net 0 (PF NULL, 0 trades), test net -3249.87 (PF 0, 1 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 94 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 37, net 8465.68, PF 1.16, validation net -426.61 (PF 0.97, 10 trades), test net 12274.17 (PF 2.1, 9 trades).
**Best validated: PUT|NoEmaGap** - validation net 7564.14, PF 1.46; held-out test net 12274.17, PF 2.1 on 9 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoEmaGap | YES | 19 | 68.79 | 1 | 18 | 7564.14 | 1.46 | 6887.01 | 9 | 12274.17 | 2.1 |
| 2 | PUT|NoEmaGap+NoPcr | YES | 31 | 9478.95 | 1.32 | 18 | 7564.14 | 1.46 | 6887.01 | 13 | 9011.18 | 1.56 |
| 3 | PUT|NoEmaGap+NoAtr | YES | 19 | 68.79 | 1 | 18 | 7564.14 | 1.46 | 6887.01 | 9 | 12274.17 | 2.1 |
| 4 | PUT|NoRsi+NoEmaGap | no | 43 | -3550.22 | 0.94 | 20 | 6034.46 | 1.32 | 6887.01 | 17 | 5916.85 | 1.3 |

