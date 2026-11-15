# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 5, net 5910.44, PF 3.64, validation net 0 (PF NULL, 0 trades), test net 5910.44 (PF 3.64, 5 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 12, net 32849.38, PF 4.64, validation net 0 (PF NULL, 0 trades), test net 38781.8 (PF 13.59, 9 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr | no | 19 | 836.61 | 1.04 | 15 | -13072.58 | 0.38 | 15120.29 | 9 | 38781.8 | 13.59 |
| 2 | CALL|NoRsi+NoPcr | no | 23 | 6639.22 | 1.27 | 15 | -13072.58 | 0.38 | 15120.29 | 14 | 39510.04 | 3.31 |
| 3 | CALL|NoPcr+NoAtr | no | 19 | 836.61 | 1.04 | 15 | -13072.58 | 0.38 | 15120.29 | 9 | 38781.8 | 13.59 |
| 4 | CALL|NoEmaGap+NoPcr | no | 89 | -14081.61 | 0.84 | 54 | -13324.77 | 0.77 | 20960.26 | 26 | 36765.71 | 2.75 |

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

Baseline: trades 35, net 35909.41, PF 2.39, validation net -2355.75 (PF 0, 3 trades), test net 43015.43 (PF 3.45, 27 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 5 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoAtr | no | 37 | -17087.06 | 0.47 | 15 | -2164.88 | 0.77 | 7458.42 | 53 | 45616.56 | 2.51 |
| 2 | CALL|NoPcr | no | 49 | -6918.22 | 0.83 | 39 | -10040.79 | 0.75 | 13684.54 | 31 | 43851.16 | 3.32 |
| 3 | CALL|NoRsi+NoPcr | no | 58 | -4587.86 | 0.91 | 41 | -12523.51 | 0.71 | 16167.26 | 33 | 62985.42 | 4.09 |
| 4 | CALL|NoPcr+NoAtr | no | 71 | -11298.43 | 0.79 | 58 | -13078.79 | 0.74 | 18088.32 | 37 | 41722.6 | 2.85 |
| 5 | CALL|NoEmaGap+NoPcr | no | 107 | -3475.79 | 0.96 | 83 | -22843.82 | 0.73 | 23882.02 | 46 | 44333.7 | 2.51 |

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 694 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 45176.12, PF 1.68; held-out test net 50345.45, PF 1.74 on 83 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 236 | 65838.39 | 1.36 | 95 | 45176.12 | 1.68 | 10040.4 | 83 | 50345.45 | 1.74 |
| 2 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.6|Atr=0.02 | YES | 123 | 16052.54 | 1.14 | 54 | 32003.9 | 2.1 | 6014.06 | 44 | 35695.73 | 1.95 |
| 3 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.7|Atr=0.02 | YES | 123 | 16052.54 | 1.14 | 54 | 32003.9 | 2.1 | 6014.06 | 44 | 35695.73 | 1.95 |
| 4 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.8|Atr=0.02 | YES | 123 | 16052.54 | 1.14 | 54 | 32003.9 | 2.1 | 6014.06 | 44 | 35695.73 | 1.95 |
| 5 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.9|Atr=0.02 | YES | 123 | 16052.54 | 1.14 | 54 | 32003.9 | 2.1 | 6014.06 | 44 | 35695.73 | 1.95 |
| 6 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.6|Atr=0.03 | YES | 118 | 21211.79 | 1.2 | 51 | 29854.98 | 2.07 | 4886.06 | 44 | 35695.73 | 1.95 |
| 7 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.7|Atr=0.03 | YES | 118 | 21211.79 | 1.2 | 51 | 29854.98 | 2.07 | 4886.06 | 44 | 35695.73 | 1.95 |
| 8 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.8|Atr=0.03 | YES | 118 | 21211.79 | 1.2 | 51 | 29854.98 | 2.07 | 4886.06 | 44 | 35695.73 | 1.95 |
| 9 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.9|Atr=0.03 | YES | 118 | 21211.79 | 1.2 | 51 | 29854.98 | 2.07 | 4886.06 | 44 | 35695.73 | 1.95 |
| 10 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 107 | 7704.77 | 1.07 | 50 | 29391.25 | 1.99 | 6668.31 | 37 | 46120.35 | 3.21 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**Best validated: PUT|NoPcr+NoAtr** - validation net 12408.34, PF 1.48; held-out test net 13273.18, PF 1.29 on 74 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+NoAtr | YES | 107 | 16067.77 | 1.27 | 61 | 12408.34 | 1.48 | 5062.18 | 74 | 13273.18 | 1.29 |
| 2 | PUT|NoRsi+NoPcr | YES | 117 | 6741.33 | 1.09 | 62 | 8863.37 | 1.25 | 8945.56 | 110 | 63136.79 | 2.08 |
| 3 | PUT|NoEmaGap+NoPcr | YES | 47 | 9084.66 | 1.28 | 33 | 7049.55 | 1.59 | 3760.19 | 54 | 21251.65 | 1.67 |
| 4 | PUT|NoPcr | YES | 47 | 9084.66 | 1.28 | 32 | 5515.11 | 1.46 | 4858.88 | 53 | 21251.65 | 1.67 |

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 34521.54, PF 1.45; held-out test net 41322.36, PF 1.39 on 141 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 244 | 58235.97 | 1.3 | 98 | 34521.54 | 1.45 | 12629.62 | 141 | 41322.36 | 1.39 |
| 2 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.9|Atr=0.03 | YES | 122 | 13458.43 | 1.12 | 55 | 23300.04 | 1.63 | 7588.54 | 74 | 13604.58 | 1.21 |
| 3 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.03 | YES | 107 | 10338.98 | 1.11 | 52 | 23051.36 | 1.64 | 10611.1 | 68 | 21079.17 | 1.4 |
| 4 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.9|Atr=0.02 | YES | 132 | 7026.12 | 1.06 | 58 | 23013.05 | 1.59 | 8716.54 | 93 | 24671.09 | 1.35 |
| 5 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.03 | YES | 108 | 10338.98 | 1.11 | 52 | 22823.34 | 1.63 | 10611.1 | 73 | 38780.42 | 1.8 |
| 6 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.03 | YES | 107 | 10338.98 | 1.11 | 52 | 22823.34 | 1.63 | 10611.1 | 71 | 22443 | 1.4 |
| 7 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.03 | YES | 108 | 10338.98 | 1.11 | 54 | 21459.65 | 1.57 | 10611.1 | 73 | 38780.42 | 1.8 |
| 8 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.7|Atr=0.03 | YES | 123 | 13458.43 | 1.12 | 56 | 21336.48 | 1.55 | 7588.54 | 79 | 18308.24 | 1.27 |
| 9 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.8|Atr=0.03 | YES | 122 | 13458.43 | 1.12 | 56 | 21336.48 | 1.55 | 7588.54 | 77 | 12221.09 | 1.18 |
| 10 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.7|Atr=0.02 | YES | 140 | 9105.77 | 1.08 | 59 | 21049.49 | 1.52 | 8716.54 | 101 | 28844.77 | 1.39 |

