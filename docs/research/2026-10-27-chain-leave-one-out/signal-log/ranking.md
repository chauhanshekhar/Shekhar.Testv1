# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 0, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 0 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 20, net 9081.95, PF 1.33, validation net -11108.68 (PF 0, 5 trades), test net 20190.63 (PF 2.2, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|LOO|NoPcr | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |

## CALL SENSEX

Baseline: trades 11, net 22814.64, PF 6.57, validation net 5582.1 (PF NULL, 2 trades), test net 18402.65 (PF 7.29, 8 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

Baseline: trades 28, net 21851.28, PF 1.68, validation net -5526.58 (PF 0.5, 7 trades), test net 28547.97 (PF 2.45, 20 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|LOO|NoPcr | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |

## PUT NIFTY

Baseline: trades 21, net 10585.1, PF 1.44, validation net -4804.25 (PF 0, 3 trades), test net 10772.27 (PF 1.63, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 196, net 47874.19, PF 1.26, validation net 15855.23 (PF 1.95, 27 trades), test net -2478.06 (PF 0.96, 57 trades).
**Best validated: PUT|LOO|NoAtr** - validation net 20344.67, PF 2.32; held-out test net -2478.06, PF 0.96 on 57 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|LOO|NoAtr | YES | 116 | 34149 | 1.33 | 28 | 20344.67 | 2.32 | 6514.18 | 57 | -2478.06 | 0.96 |
| 2 | PUT|LOO|Baseline | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 3 | PUT|LOO|NoPcr | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 67 | 2189.28 | 1.03 |
| 4 | PUT|LOO|NoCandle | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 5 | PUT|LOO|NoEmaGap | YES | 119 | 36127.13 | 1.34 | 32 | 10852.62 | 1.48 | 10487.9 | 58 | 6222.74 | 1.1 |
| 6 | PUT|LOO|NoRsi | YES | 189 | 31428.84 | 1.18 | 47 | 7197.4 | 1.18 | 9378.56 | 107 | 28079.94 | 1.24 |

## PUT SENSEX

Baseline: trades 89, net 47326.82, PF 1.92, validation net 1092.31 (PF 1.33, 5 trades), test net 37417.49 (PF 1.81, 75 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 6 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|LOO|NoPcr | no | 64 | 22139.48 | 1.7 | 31 | -250.14 | 0.98 | 7186.22 | 88 | 38073.51 | 1.62 |

## PUT ALL

Baseline: trades 261, net 82487.58, PF 1.37, validation net 14412.38 (PF 1.64, 34 trades), test net 37107.91 (PF 1.39, 109 trades).
**Best validated: PUT|LOO|NoRsi** - validation net 23863.04, PF 1.5; held-out test net 61438.25, PF 1.4 on 174 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|LOO|NoRsi | YES | 197 | 32723.79 | 1.18 | 67 | 23863.04 | 1.5 | 9378.56 | 174 | 61438.25 | 1.4 |
| 2 | PUT|LOO|NoAtr | YES | 135 | 31464.87 | 1.28 | 43 | 23598.69 | 2.02 | 6514.18 | 111 | 40411.36 | 1.42 |
| 3 | PUT|LOO|NoPcr | YES | 152 | 24591.25 | 1.19 | 50 | 17158.82 | 1.65 | 7630.65 | 123 | 50346.2 | 1.47 |
| 4 | PUT|LOO|Baseline | YES | 118 | 30967.3 | 1.29 | 34 | 14412.38 | 1.64 | 6514.18 | 109 | 37107.91 | 1.39 |
| 5 | PUT|LOO|NoCandle | YES | 118 | 30967.3 | 1.29 | 34 | 14412.38 | 1.64 | 6514.18 | 109 | 37107.91 | 1.39 |
| 6 | PUT|LOO|NoEmaGap | YES | 125 | 32597.41 | 1.29 | 39 | 9409.76 | 1.33 | 10487.9 | 110 | 45858.34 | 1.49 |

