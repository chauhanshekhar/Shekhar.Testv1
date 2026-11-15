# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 0, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 0 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 31 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 20, net 9081.95, PF 1.33, validation net -11108.68 (PF 0, 5 trades), test net 20190.63 (PF 2.2, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 31 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoRsi+NoPcr | no | 49 | 5133.72 | 1.11 | 25 | -14057.44 | 0.52 | 14057.34 | 20 | 27132.59 | 2.34 |
| 2 | CALL|NoPcr+NoAtr | no | 46 | 7489.43 | 1.17 | 24 | -16515.69 | 0.43 | 16515.59 | 20 | 27132.59 | 2.34 |
| 3 | CALL|NoPcr | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 4 | CALL|NoEmaGap+NoPcr | no | 63 | 33998.58 | 1.73 | 27 | -17043.37 | 0.47 | 17043.27 | 25 | 24881.17 | 1.79 |

## CALL SENSEX

Baseline: trades 11, net 22814.64, PF 6.57, validation net 5582.1 (PF NULL, 2 trades), test net 18402.65 (PF 7.29, 8 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 31 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | no | 51 | -4717.92 | 0.86 | 21 | 14411.28 | 2.44 | 2614.76 | 20 | 14142.48 | 1.95 |

## CALL ALL

Baseline: trades 28, net 21851.28, PF 1.68, validation net -5526.58 (PF 0.5, 7 trades), test net 28547.97 (PF 2.45, 20 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 31 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | no | 77 | 8081.59 | 1.13 | 40 | -1801.14 | 0.95 | 12225.05 | 33 | 39518.85 | 2.45 |
| 2 | CALL|NoRsi+NoPcr | no | 67 | 7125.63 | 1.13 | 37 | -1943.6 | 0.95 | 11319.54 | 31 | 39288.48 | 2.49 |
| 3 | CALL|NoPcr | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 4 | CALL|NoEmaGap+NoPcr | no | 78 | 34776.36 | 1.66 | 39 | -5714.27 | 0.86 | 12335.19 | 34 | 39767.47 | 2.22 |

## PUT NIFTY

Baseline: trades 21, net 10585.1, PF 1.44, validation net -4804.25 (PF 0, 3 trades), test net 10772.27 (PF 1.63, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 28 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 196, net 47874.19, PF 1.26, validation net 15855.23 (PF 1.95, 27 trades), test net -2478.06 (PF 0.96, 57 trades).
**Best validated: PUT|NoAtr** - validation net 20344.67, PF 2.32; held-out test net -2478.06, PF 0.96 on 57 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoAtr | YES | 116 | 34149 | 1.33 | 28 | 20344.67 | 2.32 | 6514.18 | 57 | -2478.06 | 0.96 |
| 2 | PUT|AtrMin=0.02 | YES | 116 | 34149 | 1.33 | 28 | 20344.67 | 2.32 | 6514.18 | 57 | -2478.06 | 0.96 |
| 3 | PUT|NoPcr+NoAtr | YES | 116 | 34149 | 1.33 | 28 | 20344.67 | 2.32 | 6514.18 | 67 | 2189.28 | 1.03 |
| 4 | PUT|Baseline | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 5 | PUT|NoPcr | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 67 | 2189.28 | 1.03 |
| 6 | PUT|RsiFloor=28 | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 7 | PUT|RsiCeiling=38 | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 8 | PUT|PcrMin=0.6 | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 9 | PUT|PcrMin=0.7 | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 10 | PUT|PcrMin=0.8 | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |

## PUT SENSEX

Baseline: trades 89, net 47326.82, PF 1.92, validation net 1092.31 (PF 1.33, 5 trades), test net 37417.49 (PF 1.81, 75 trades).
**Best validated: PUT|NoRsi+NoPcr** - validation net 18524.4, PF 1.51; held-out test net 59484.27, PF 1.54 on 163 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoPcr | YES | 135 | 14291.92 | 1.16 | 65 | 18524.4 | 1.51 | 5004.76 | 163 | 59484.27 | 1.54 |
| 2 | PUT|NoRsi+NoAtr | YES | 55 | 21481.11 | 2.01 | 30 | 17531.59 | 2.31 | 3599.65 | 141 | 51512.33 | 1.57 |
| 3 | PUT|NoPcr+NoAtr | YES | 138 | 46458.51 | 1.74 | 75 | 13673.41 | 1.42 | 6066.57 | 93 | 48489.14 | 1.81 |
| 4 | PUT|NoPcr | no | 64 | 22139.48 | 1.7 | 31 | -250.14 | 0.98 | 7186.22 | 88 | 38073.51 | 1.62 |
| 5 | PUT|NoEmaGap+NoPcr | no | 64 | 22139.48 | 1.7 | 31 | -250.14 | 0.98 | 7186.22 | 88 | 38073.51 | 1.62 |

## PUT ALL

Baseline: trades 261, net 82487.58, PF 1.37, validation net 14412.38 (PF 1.64, 34 trades), test net 37107.91 (PF 1.39, 109 trades).
**Best validated: PUT|NoRsi+NoAtr** - validation net 32249.71, PF 1.61; held-out test net 55293.61, PF 1.35 on 182 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoAtr | YES | 217 | 34425.15 | 1.18 | 82 | 32249.71 | 1.61 | 8238.05 | 182 | 55293.61 | 1.35 |
| 2 | PUT|NoPcr+NoAtr | YES | 193 | 34001.26 | 1.24 | 88 | 25576.23 | 1.6 | 7630.65 | 125 | 60925.44 | 1.6 |
| 3 | PUT|NoRsi | YES | 197 | 32723.79 | 1.18 | 67 | 23863.04 | 1.5 | 9378.56 | 173 | 63557.03 | 1.42 |
| 4 | PUT|NoAtr | YES | 135 | 31464.87 | 1.28 | 43 | 23598.69 | 2.02 | 6514.18 | 111 | 40411.36 | 1.42 |
| 5 | PUT|AtrMin=0.02 | YES | 133 | 37303.18 | 1.34 | 43 | 23598.69 | 2.02 | 6514.18 | 111 | 40411.36 | 1.42 |
| 6 | PUT|NoRsi+NoEmaGap | YES | 213 | 42789.2 | 1.23 | 75 | 21633.65 | 1.38 | 11521.15 | 180 | 70498.99 | 1.45 |
| 7 | PUT|NoEmaGap+NoAtr | YES | 143 | 36347.56 | 1.31 | 49 | 17738.72 | 1.59 | 8687.8 | 112 | 46800.01 | 1.5 |
| 8 | PUT|NoPcr | YES | 152 | 24591.25 | 1.19 | 50 | 17158.82 | 1.65 | 7630.65 | 123 | 50346.2 | 1.47 |
| 9 | PUT|PcrMin=0.8 | YES | 117 | 30967.3 | 1.29 | 33 | 15419.05 | 1.72 | 6514.18 | 109 | 37145.39 | 1.39 |
| 10 | PUT|Rsi=28-38+Pcr=0.8 | YES | 117 | 30967.3 | 1.29 | 33 | 15419.05 | 1.72 | 6514.18 | 109 | 37145.39 | 1.39 |

