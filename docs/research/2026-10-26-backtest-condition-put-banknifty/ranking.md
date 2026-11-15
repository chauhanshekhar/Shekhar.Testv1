# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 28, net 19043.54, PF 1.65, validation net -4818.16 (PF 0, 3 trades), test net 16302.81 (PF 1.74, 21 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 694 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 215, net 71397.53, PF 1.37, validation net 18388.33 (PF 1.82, 34 trades), test net 23290.9 (PF 1.33, 66 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 34524.78, PF 1.52; held-out test net 65575.44, PF 1.34 on 193 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 311 | 62136.74 | 1.25 | 85 | 34524.78 | 1.52 | 20451.54 | 193 | 65575.44 | 1.34 |
| 2 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 150 | 16403.92 | 1.12 | 39 | 32697.15 | 2.54 | 8282.06 | 77 | 30243.46 | 1.38 |
| 3 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 150 | 16403.92 | 1.12 | 39 | 32697.15 | 2.54 | 8282.06 | 77 | 30243.46 | 1.38 |
| 4 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.02 | YES | 150 | 16403.92 | 1.12 | 39 | 32697.15 | 2.54 | 8282.06 | 77 | 30243.46 | 1.38 |
| 5 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.02 | YES | 150 | 16403.92 | 1.12 | 39 | 32697.15 | 2.54 | 8282.06 | 77 | 30243.46 | 1.38 |
| 6 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.6|Atr=0.02 | YES | 135 | 20940.84 | 1.18 | 37 | 29332.66 | 2.31 | 8002.78 | 69 | 25327.41 | 1.33 |
| 7 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.7|Atr=0.02 | YES | 135 | 20940.84 | 1.18 | 37 | 29332.66 | 2.31 | 8002.78 | 69 | 25327.41 | 1.33 |
| 8 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.8|Atr=0.02 | YES | 135 | 20940.84 | 1.18 | 37 | 29332.66 | 2.31 | 8002.78 | 69 | 25327.41 | 1.33 |
| 9 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.9|Atr=0.02 | YES | 135 | 20940.84 | 1.18 | 37 | 29332.66 | 2.31 | 8002.78 | 69 | 25327.41 | 1.33 |
| 10 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.03 | YES | 142 | 13668.45 | 1.1 | 38 | 29314.39 | 2.38 | 8282.06 | 77 | 30243.46 | 1.38 |

## PUT SENSEX

Baseline: trades 107, net 70348.01, PF 2.18, validation net 1144.52 (PF 1.34, 5 trades), test net 56548 (PF 2, 91 trades).
**Best validated: PUT|NoRsi+NoPcr** - validation net 13273.57, PF 1.29; held-out test net 92236.13, PF 1.68 on 206 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoPcr | YES | 158 | 28511.71 | 1.3 | 82 | 13273.57 | 1.29 | 9463.23 | 206 | 92236.13 | 1.68 |
| 2 | PUT|NoRsi+NoAtr | YES | 74 | 19238.26 | 1.64 | 23 | 12870.03 | 2.41 | 3593.43 | 187 | 91860.53 | 1.79 |
| 3 | PUT|NoPcr+NoAtr | YES | 162 | 47809.3 | 1.66 | 98 | 11008.7 | 1.24 | 9440.03 | 115 | 69317.13 | 2.03 |
| 4 | PUT|Grid|Rsi=25-40|Gap=10|Pcr=0.7|Atr=0.02 | YES | 49 | 18259.81 | 2.04 | 15 | 9765.32 | 3.06 | 1578.06 | 116 | 64109.45 | 1.87 |
| 5 | PUT|Grid|Rsi=25-38|Gap=10|Pcr=0.7|Atr=0.02 | YES | 46 | 20689.21 | 2.41 | 15 | 9662.66 | 3.01 | 1578.06 | 110 | 63182.13 | 1.94 |
| 6 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.7|Atr=0.02 | YES | 49 | 20032.66 | 2.07 | 16 | 9261.98 | 2.55 | 1578.06 | 115 | 60089.8 | 1.83 |
| 7 | PUT|Grid|Rsi=25-42|Gap=10|Pcr=0.7|Atr=0.02 | YES | 51 | 16221.98 | 1.82 | 17 | 8650 | 2.32 | 2569.78 | 126 | 61017.96 | 1.75 |
| 8 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 54 | 15944.8 | 1.69 | 17 | 8461.35 | 2.25 | 1935.56 | 125 | 52579.8 | 1.63 |
| 9 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.6|Atr=0.02 | YES | 53 | 20044.44 | 1.97 | 17 | 8249.04 | 2.18 | 2045.2 | 115 | 59924.26 | 1.8 |
| 10 | PUT|Grid|Rsi=25-40|Gap=10|Pcr=0.6|Atr=0.02 | YES | 52 | 19363.63 | 2.05 | 17 | 7587.2 | 2.1 | 2045.2 | 116 | 63943.91 | 1.84 |

## PUT ALL

Baseline: trades 288, net 112942.73, PF 1.49, validation net 14714.68 (PF 1.48, 41 trades), test net 59670.4 (PF 1.57, 124 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 38957.29, PF 1.48; held-out test net 94768.17, PF 1.42 on 254 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 320 | 56102.03 | 1.22 | 106 | 38957.29 | 1.48 | 22727.79 | 254 | 94768.17 | 1.42 |
| 2 | PUT|NoEmaGap+NoAtr | YES | 184 | 53300.72 | 1.43 | 65 | 37463.62 | 1.9 | 6733.52 | 146 | 83539.2 | 1.77 |
| 3 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 178 | 23743.95 | 1.16 | 58 | 36340.33 | 2.11 | 8347.52 | 159 | 54182.47 | 1.39 |
| 4 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 182 | 23640.13 | 1.16 | 59 | 35327.4 | 2.05 | 8347.52 | 159 | 54182.47 | 1.39 |
| 5 | PUT|NoRsi+NoAtr | YES | 237 | 17410.75 | 1.09 | 81 | 33979.42 | 1.65 | 17369.27 | 223 | 89514.32 | 1.48 |
| 6 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.7|Atr=0.02 | YES | 160 | 36135.71 | 1.3 | 55 | 33776.47 | 2.02 | 8002.78 | 142 | 64301.48 | 1.54 |
| 7 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.02 | YES | 171 | 25136.55 | 1.18 | 53 | 33442.06 | 2.08 | 8347.52 | 156 | 53230.14 | 1.39 |
| 8 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.6|Atr=0.02 | YES | 164 | 36009.49 | 1.29 | 56 | 32763.54 | 1.96 | 8002.78 | 143 | 61890.62 | 1.51 |
| 9 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.02 | YES | 163 | 18457.53 | 1.13 | 50 | 31173.95 | 2.05 | 8347.52 | 156 | 49283.1 | 1.36 |
| 10 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.8|Atr=0.02 | YES | 155 | 34936.35 | 1.29 | 51 | 29974.91 | 1.93 | 8002.78 | 139 | 62729.4 | 1.53 |

