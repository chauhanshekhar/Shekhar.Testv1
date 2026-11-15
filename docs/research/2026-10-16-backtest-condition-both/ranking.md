# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 6, net 14189.34, PF 7.35, validation net 0 (PF NULL, 0 trades), test net 14189.34 (PF 7.35, 6 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 13, net 43161.08, PF 5.79, validation net 0 (PF NULL, 0 trades), test net 49093.5 (PF 16.93, 10 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr | no | 19 | 836.61 | 1.04 | 15 | -13072.58 | 0.38 | 15120.29 | 10 | 49093.5 | 16.93 |
| 2 | CALL|NoRsi+NoPcr | no | 23 | 6639.22 | 1.27 | 15 | -13072.58 | 0.38 | 15120.29 | 14 | 39510.04 | 3.31 |
| 3 | CALL|NoPcr+NoAtr | no | 19 | 836.61 | 1.04 | 15 | -13072.58 | 0.38 | 15120.29 | 10 | 49093.5 | 16.93 |
| 4 | CALL|NoEmaGap+NoPcr | no | 89 | -14081.61 | 0.84 | 54 | -13324.77 | 0.77 | 20960.26 | 27 | 47077.41 | 3.24 |

## CALL SENSEX

Baseline: trades 29, net 6749.41, PF 1.36, validation net -2355.75 (PF 0, 3 trades), test net 7923.02 (PF 1.49, 24 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 5 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoAtr | no | 18 | -6193.33 | 0.44 | 15 | -2164.88 | 0.77 | 7458.42 | 41 | 502.93 | 1.02 |
| 2 | CALL|NoPcr | no | 37 | 3723.91 | 1.17 | 33 | -5010.28 | 0.82 | 7817.71 | 29 | 4159.54 | 1.19 |
| 3 | CALL|NoPcr+NoAtr | no | 66 | -10364.74 | 0.77 | 54 | -6279.93 | 0.84 | 12958.83 | 35 | 2030.99 | 1.08 |
| 4 | CALL|NoRsi+NoPcr | no | 42 | 4635.1 | 1.19 | 35 | -7493 | 0.76 | 10300.43 | 31 | 24353.92 | 2.12 |
| 5 | CALL|NoEmaGap+NoPcr | no | 46 | 2789.04 | 1.1 | 43 | -11243.98 | 0.69 | 14918.06 | 34 | 637.61 | 1.03 |

## CALL ALL

Baseline: trades 36, net 44188.31, PF 2.71, validation net -2355.75 (PF 0, 3 trades), test net 51294.34 (PF 3.93, 28 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 5 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoAtr | no | 37 | -17087.06 | 0.47 | 15 | -2164.88 | 0.77 | 7458.42 | 54 | 53895.47 | 2.78 |
| 2 | CALL|NoPcr | no | 49 | -6918.22 | 0.83 | 39 | -10040.79 | 0.75 | 13684.54 | 32 | 52130.06 | 3.76 |
| 3 | CALL|NoRsi+NoPcr | no | 58 | -4587.86 | 0.91 | 41 | -12523.51 | 0.71 | 16167.26 | 33 | 62985.42 | 4.09 |
| 4 | CALL|NoPcr+NoAtr | no | 71 | -11298.43 | 0.79 | 58 | -13078.79 | 0.74 | 18088.32 | 38 | 50001.51 | 3.22 |
| 5 | CALL|NoEmaGap+NoPcr | no | 107 | -3475.79 | 0.96 | 83 | -22843.82 | 0.73 | 23882.02 | 47 | 52612.61 | 2.79 |

## PUT NIFTY

Baseline: trades 16, net -4021.29, PF 0.82, validation net -6968.9 (PF 0, 4 trades), test net -6750.47 (PF 0.57, 9 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 694 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 104, net 58720.72, PF 1.68, validation net 641.01 (PF 1.03, 26 trades), test net 43408.18 (PF 4.42, 25 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 45176.12, PF 1.68; held-out test net 50345.45, PF 1.74 on 83 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 236 | 65838.39 | 1.36 | 95 | 45176.12 | 1.68 | 10040.4 | 83 | 50345.45 | 1.74 |
| 2 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.6|Atr=0.02 | YES | 121 | 31758.03 | 1.3 | 50 | 20905.11 | 1.63 | 9154.29 | 43 | 47348.04 | 2.5 |
| 3 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.7|Atr=0.02 | YES | 121 | 31758.03 | 1.3 | 50 | 20905.11 | 1.63 | 9154.29 | 43 | 47348.04 | 2.5 |
| 4 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.8|Atr=0.02 | YES | 121 | 31758.03 | 1.3 | 50 | 20905.11 | 1.63 | 9154.29 | 43 | 47348.04 | 2.5 |
| 5 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.9|Atr=0.02 | YES | 121 | 31758.03 | 1.3 | 50 | 20905.11 | 1.63 | 9154.29 | 43 | 47348.04 | 2.5 |
| 6 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 112 | 41031.45 | 1.44 | 47 | 19349.96 | 1.57 | 8582.98 | 38 | 55119.6 | 3.82 |
| 7 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 112 | 41031.45 | 1.44 | 47 | 19349.96 | 1.57 | 8582.98 | 38 | 55119.6 | 3.82 |
| 8 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.02 | YES | 112 | 41031.45 | 1.44 | 47 | 19349.96 | 1.57 | 8582.98 | 38 | 55119.6 | 3.82 |
| 9 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.02 | YES | 112 | 41031.45 | 1.44 | 47 | 19349.96 | 1.57 | 8582.98 | 38 | 55119.6 | 3.82 |
| 10 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.6|Atr=0.03 | YES | 116 | 33969.44 | 1.34 | 47 | 19291.99 | 1.6 | 8026.29 | 43 | 47348.04 | 2.5 |

## PUT SENSEX

Baseline: trades 69, net 44314.18, PF 2.34, validation net 3451.5 (PF NULL, 2 trades), test net 32716.96 (PF 2.03, 58 trades).
**Best validated: PUT|NoPcr+NoAtr** - validation net 15916.3, PF 1.51; held-out test net 34569.09, PF 1.81 on 83 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+NoAtr | YES | 124 | 15746.04 | 1.23 | 72 | 15916.3 | 1.51 | 5043.02 | 83 | 34569.09 | 1.81 |
| 2 | PUT|NoEmaGap+NoPcr | YES | 56 | 465.51 | 1.01 | 42 | 13037.72 | 1.79 | 6139.19 | 65 | 39895.04 | 2.27 |
| 3 | PUT|NoRsi+NoPcr | YES | 117 | 6741.33 | 1.09 | 62 | 8863.37 | 1.25 | 8945.56 | 110 | 63136.79 | 2.08 |
| 4 | PUT|NoPcr | YES | 49 | 9640.51 | 1.29 | 39 | 6898.64 | 1.4 | 7382.59 | 63 | 36847.55 | 2.13 |

## PUT ALL

Baseline: trades 152, net 30379.36, PF 1.24, validation net -4758.65 (PF 0.85, 30 trades), test net 17255.23 (PF 1.35, 64 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 34521.54, PF 1.45; held-out test net 41322.36, PF 1.39 on 141 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 244 | 58235.97 | 1.3 | 98 | 34521.54 | 1.45 | 12629.62 | 141 | 41322.36 | 1.39 |
| 2 | PUT|NoEmaGap+NoPcr | YES | 139 | 33722.82 | 1.3 | 78 | 22118.05 | 1.41 | 10002.23 | 83 | 25969.31 | 1.4 |
| 3 | PUT|NoEmaGap+NoAtr | YES | 137 | 56479.42 | 1.62 | 66 | 16764.42 | 1.32 | 12131.9 | 90 | 34310.02 | 1.57 |
| 4 | PUT|Grid|Rsi=25-40|Gap=20|Pcr=0.6|Atr=0.02 | YES | 52 | 14649.23 | 1.4 | 15 | 16205.4 | 2.56 | 4483.09 | 81 | 10079.14 | 1.17 |
| 5 | PUT|Grid|Rsi=25-42|Gap=20|Pcr=0.6|Atr=0.02 | YES | 52 | 14647.39 | 1.4 | 15 | 16205.4 | 2.56 | 4483.09 | 85 | 16865.03 | 1.28 |
| 6 | PUT|Grid|Rsi=25-38|Gap=20|Pcr=0.6|Atr=0.02 | YES | 50 | 16074.15 | 1.46 | 15 | 16171.19 | 2.56 | 4483.09 | 78 | 12640.1 | 1.22 |
| 7 | PUT|NoPcr+NoAtr | YES | 142 | 23534.82 | 1.24 | 79 | 15749.22 | 1.38 | 6123.11 | 86 | 26814.66 | 1.45 |
| 8 | PUT|Grid|Rsi=25-40|Gap=20|Pcr=0.7|Atr=0.02 | YES | 52 | 14649.23 | 1.4 | 15 | 15690.73 | 2.51 | 4483.09 | 79 | 8547.82 | 1.14 |
| 9 | PUT|Grid|Rsi=25-42|Gap=20|Pcr=0.7|Atr=0.02 | YES | 52 | 14647.39 | 1.4 | 15 | 15690.73 | 2.51 | 4483.09 | 83 | 15333.71 | 1.25 |
| 10 | PUT|Grid|Rsi=25-38|Gap=20|Pcr=0.7|Atr=0.02 | YES | 50 | 16074.15 | 1.46 | 15 | 15656.52 | 2.51 | 4483.09 | 76 | 11108.78 | 1.2 |

