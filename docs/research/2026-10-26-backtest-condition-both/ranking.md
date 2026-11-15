# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 12, net 20675.34, PF 2.74, validation net 2406.38 (PF NULL, 1 trades), test net 18268.95 (PF 2.54, 11 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 36, net 31802.61, PF 1.59, validation net 0 (PF NULL, 0 trades), test net 34795.6 (PF 1.68, 34 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | no | 51 | 79.82 | 1 | 17 | -6184.53 | 0.65 | 11075.47 | 42 | 40928.83 | 1.69 |
| 2 | CALL|NoPcr | no | 51 | 79.82 | 1 | 17 | -6287.66 | 0.65 | 11178.6 | 42 | 40928.83 | 1.69 |
| 3 | CALL|NoEmaGap+NoPcr | no | 76 | 18456.05 | 1.28 | 22 | -6575.38 | 0.71 | 13287.22 | 51 | 37938.11 | 1.49 |
| 4 | CALL|NoRsi+NoPcr | no | 64 | 6697.68 | 1.11 | 24 | -11109.74 | 0.58 | 16304.71 | 48 | 52781.14 | 1.84 |

## CALL SENSEX

Baseline: trades 25, net 37188.08, PF 4.45, validation net 7710.67 (PF NULL, 3 trades), test net 27659.42 (PF 3.88, 18 trades).
**Best validated: CALL|NoRsi+NoPcr** - validation net 3872.66, PF 1.13; held-out test net 45517.49, PF 2.17 on 47 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoRsi+NoPcr | YES | 58 | 2433.98 | 1.07 | 38 | 3872.66 | 1.13 | 8574.65 | 47 | 45517.49 | 2.17 |
| 2 | CALL|NoPcr | no | 42 | -10649.02 | 0.62 | 33 | 5061.62 | 1.18 | 6615.38 | 29 | 19380.98 | 1.78 |
| 3 | CALL|NoEmaGap+NoPcr | no | 47 | -4796.36 | 0.82 | 37 | 2416.4 | 1.08 | 9331.23 | 30 | 19198.79 | 1.74 |
| 4 | CALL|NoPcr+NoAtr | no | 80 | -3065.55 | 0.93 | 49 | 1663.24 | 1.04 | 13720.38 | 29 | 19380.98 | 1.78 |

## CALL ALL

Baseline: trades 52, net 54119.6, PF 1.85, validation net 7979.11 (PF NULL, 3 trades), test net 47315.49 (PF 1.8, 43 trades).
**Best validated: CALL|NoPcr+NoAtr** - validation net 3532.08, PF 1.08; held-out test net 53980.13, PF 1.8 on 53 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | YES | 99 | 8950.9 | 1.13 | 53 | 3532.08 | 1.08 | 11389.34 | 53 | 53980.13 | 1.8 |
| 2 | CALL|NoPcr | YES | 75 | 4810.33 | 1.08 | 44 | 483.66 | 1.01 | 12484.37 | 53 | 53980.13 | 1.8 |
| 3 | CALL|NoEmaGap+NoPcr | no | 101 | 26614.2 | 1.36 | 53 | -1732.54 | 0.96 | 14590.83 | 62 | 47853.89 | 1.57 |
| 4 | CALL|NoRsi+NoPcr | no | 90 | 24340.44 | 1.36 | 56 | -8017.12 | 0.85 | 20671.3 | 73 | 56617.54 | 1.65 |

## PUT NIFTY

Baseline: trades 28, net 19043.54, PF 1.65, validation net -4818.16 (PF 0, 3 trades), test net 16302.81 (PF 1.74, 21 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 694 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 215, net 71562.07, PF 1.37, validation net 18426.33 (PF 1.81, 34 trades), test net 23392.04 (PF 1.33, 66 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 34568.45, PF 1.52; held-out test net 65769.92, PF 1.34 on 193 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 311 | 62141.25 | 1.24 | 85 | 34568.45 | 1.52 | 20527.66 | 193 | 65769.92 | 1.34 |
| 2 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 150 | 16369.8 | 1.12 | 39 | 32774.8 | 2.54 | 8308.09 | 77 | 30354.59 | 1.38 |
| 3 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 150 | 16369.8 | 1.12 | 39 | 32774.8 | 2.54 | 8308.09 | 77 | 30354.59 | 1.38 |
| 4 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.02 | YES | 150 | 16369.8 | 1.12 | 39 | 32774.8 | 2.54 | 8308.09 | 77 | 30354.59 | 1.38 |
| 5 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.02 | YES | 150 | 16369.8 | 1.12 | 39 | 32774.8 | 2.54 | 8308.09 | 77 | 30354.59 | 1.38 |
| 6 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.6|Atr=0.02 | YES | 135 | 20939.61 | 1.18 | 37 | 29402.14 | 2.31 | 8027.93 | 69 | 25438.53 | 1.33 |
| 7 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.7|Atr=0.02 | YES | 135 | 20939.61 | 1.18 | 37 | 29402.14 | 2.31 | 8027.93 | 69 | 25438.53 | 1.33 |
| 8 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.8|Atr=0.02 | YES | 135 | 20939.61 | 1.18 | 37 | 29402.14 | 2.31 | 8027.93 | 69 | 25438.53 | 1.33 |
| 9 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.9|Atr=0.02 | YES | 135 | 20939.61 | 1.18 | 37 | 29402.14 | 2.31 | 8027.93 | 69 | 25438.53 | 1.33 |
| 10 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.03 | YES | 142 | 13648.09 | 1.1 | 38 | 29387.03 | 2.38 | 8308.09 | 77 | 30354.59 | 1.38 |

## PUT SENSEX

Baseline: trades 107, net 70348.01, PF 2.18, validation net 1144.52 (PF 1.34, 5 trades), test net 56548 (PF 2, 91 trades).
**Best validated: PUT|NoRsi+NoPcr** - validation net 13273.57, PF 1.29; held-out test net 91959.96, PF 1.67 on 206 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoPcr | YES | 158 | 28511.71 | 1.3 | 82 | 13273.57 | 1.29 | 9463.23 | 206 | 91959.96 | 1.67 |
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

Baseline: trades 288, net 113058.21, PF 1.49, validation net 14752.68 (PF 1.48, 41 trades), test net 59744.17 (PF 1.57, 124 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 39000.97, PF 1.48; held-out test net 94953.6, PF 1.42 on 254 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 320 | 56054.68 | 1.22 | 106 | 39000.97 | 1.48 | 22803.91 | 254 | 94953.6 | 1.42 |
| 2 | PUT|NoEmaGap+NoAtr | YES | 184 | 53327.59 | 1.43 | 65 | 37519.21 | 1.9 | 6760.68 | 146 | 83636.83 | 1.77 |
| 3 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 178 | 23684.26 | 1.16 | 58 | 36417.98 | 2.11 | 8366.64 | 159 | 54273.97 | 1.39 |
| 4 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 182 | 23580.44 | 1.16 | 59 | 35405.05 | 2.05 | 8366.64 | 159 | 54273.97 | 1.39 |
| 5 | PUT|NoRsi+NoAtr | YES | 237 | 17346.48 | 1.09 | 81 | 34030.94 | 1.65 | 17417.03 | 223 | 89655.35 | 1.48 |
| 6 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.7|Atr=0.02 | YES | 160 | 36118.75 | 1.3 | 55 | 33845.95 | 2.02 | 8027.93 | 142 | 64392.98 | 1.54 |
| 7 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.02 | YES | 171 | 25073.95 | 1.18 | 53 | 33519.71 | 2.08 | 8366.64 | 156 | 53321.65 | 1.39 |
| 8 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.6|Atr=0.02 | YES | 164 | 35992.52 | 1.29 | 56 | 32833.02 | 1.96 | 8027.93 | 143 | 61982.12 | 1.51 |
| 9 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.02 | YES | 163 | 18391.58 | 1.13 | 50 | 31251.61 | 2.05 | 8366.64 | 156 | 49374.6 | 1.36 |
| 10 | PUT|Grid|Rsi=25-38|Gap=5|Pcr=0.8|Atr=0.02 | YES | 155 | 34916.48 | 1.29 | 51 | 30044.39 | 1.93 | 8027.93 | 139 | 62820.9 | 1.53 |

