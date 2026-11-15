# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 7, net 16778.25, PF 8.63, validation net -2198.86 (PF 0, 1 trades), test net 18977.11 (PF NULL, 6 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 36, net 32336.37, PF 1.82, validation net 0 (PF NULL, 0 trades), test net 42076.36 (PF 2.63, 24 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | no | 52 | 8259.59 | 1.17 | 32 | -8570.21 | 0.74 | 16132.69 | 25 | 41869.23 | 2.61 |
| 2 | CALL|NoPcr | no | 52 | 8259.59 | 1.17 | 32 | -8672.94 | 0.73 | 16235.42 | 25 | 41869.23 | 2.61 |
| 3 | CALL|NoRsi+NoPcr | no | 56 | 3223.4 | 1.06 | 32 | -8672.94 | 0.73 | 16235.42 | 26 | 47850.4 | 2.84 |
| 4 | CALL|NoEmaGap+NoPcr | no | 105 | 5263.89 | 1.05 | 57 | -12738.18 | 0.78 | 26511.96 | 35 | 45529.95 | 2.26 |

## CALL SENSEX

Baseline: trades 32, net 18234.63, PF 2.08, validation net 2963.99 (PF NULL, 3 trades), test net 15247 (PF 1.97, 26 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 17 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr | no | 46 | -1319.86 | 0.96 | 39 | 2604.56 | 1.09 | 6992.53 | 27 | 13770.22 | 1.74 |
| 2 | CALL|NoRsi+NoPcr | no | 51 | -603.03 | 0.98 | 40 | 1162.75 | 1.04 | 8499.15 | 29 | 31856.89 | 2.7 |
| 3 | CALL|NoPcr+NoAtr | no | 86 | -97.56 | 1 | 65 | -292.88 | 0.99 | 14751.11 | 32 | 8814.68 | 1.39 |
| 4 | CALL|NoEmaGap+NoAtr | no | 27 | -269.08 | 0.98 | 19 | -879.49 | 0.91 | 5548.96 | 41 | 6745.98 | 1.26 |
| 5 | CALL|Grid|Rsi=40-65|Gap=5|Pcr=0.7|Atr=0.02 | no | 23 | 2051.59 | 1.21 | 16 | -1322.98 | 0.83 | 5836.03 | 42 | 13642.31 | 1.55 |
| 6 | CALL|Grid|Rsi=40-70|Gap=5|Pcr=0.7|Atr=0.02 | no | 24 | 4177.2 | 1.42 | 16 | -1322.98 | 0.83 | 5836.03 | 42 | 13642.31 | 1.55 |
| 7 | CALL|Grid|Rsi=40-75|Gap=5|Pcr=0.7|Atr=0.02 | no | 24 | 4177.2 | 1.42 | 16 | -1322.98 | 0.83 | 5836.03 | 43 | 21372.8 | 1.86 |
| 8 | CALL|Grid|Rsi=45-65|Gap=5|Pcr=0.7|Atr=0.02 | no | 23 | 2120.28 | 1.22 | 16 | -1322.98 | 0.83 | 5836.03 | 39 | 10139.32 | 1.45 |
| 9 | CALL|Grid|Rsi=45-70|Gap=5|Pcr=0.7|Atr=0.02 | no | 24 | 4245.89 | 1.43 | 16 | -1322.98 | 0.83 | 5836.03 | 39 | 10139.32 | 1.45 |
| 10 | CALL|Grid|Rsi=45-75|Gap=5|Pcr=0.7|Atr=0.02 | no | 24 | 4245.89 | 1.43 | 16 | -1322.98 | 0.83 | 5836.03 | 40 | 17869.81 | 1.79 |

## CALL ALL

Baseline: trades 59, net 42084.24, PF 1.86, validation net 765.13 (PF 1.35, 4 trades), test net 51035.47 (PF 2.61, 40 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 17 of 700 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoEmaGap+NoAtr | no | 51 | -11539.6 | 0.71 | 19 | -879.49 | 0.91 | 5548.96 | 60 | 50170.96 | 2.19 |
| 2 | CALL|Grid|Rsi=40-65|Gap=5|Pcr=0.7|Atr=0.02 | no | 36 | -12589.66 | 0.58 | 15 | -2370.4 | 0.73 | 6883.47 | 56 | 52783.01 | 2.44 |
| 3 | CALL|Grid|Rsi=40-70|Gap=5|Pcr=0.7|Atr=0.02 | no | 37 | -3485.49 | 0.85 | 15 | -2370.4 | 0.73 | 6883.47 | 56 | 51284.07 | 2.34 |
| 4 | CALL|Grid|Rsi=40-75|Gap=5|Pcr=0.7|Atr=0.02 | no | 37 | -3485.49 | 0.85 | 15 | -2370.4 | 0.73 | 6883.47 | 56 | 51284.07 | 2.34 |
| 5 | CALL|Grid|Rsi=45-65|Gap=5|Pcr=0.7|Atr=0.02 | no | 36 | -12520.96 | 0.59 | 15 | -2370.4 | 0.73 | 6883.47 | 52 | 50194.53 | 2.51 |
| 6 | CALL|Grid|Rsi=45-70|Gap=5|Pcr=0.7|Atr=0.02 | no | 37 | -3416.79 | 0.85 | 15 | -2370.4 | 0.73 | 6883.47 | 52 | 48695.59 | 2.4 |
| 7 | CALL|Grid|Rsi=45-75|Gap=5|Pcr=0.7|Atr=0.02 | no | 37 | -3416.79 | 0.85 | 15 | -2370.4 | 0.73 | 6883.47 | 52 | 48695.59 | 2.4 |
| 8 | CALL|Grid|Rsi=50-65|Gap=5|Pcr=0.7|Atr=0.02 | no | 32 | -8551.66 | 0.67 | 15 | -2374.39 | 0.73 | 6887.46 | 46 | 50076.11 | 2.65 |
| 9 | CALL|Grid|Rsi=50-70|Gap=5|Pcr=0.7|Atr=0.02 | no | 33 | 552.52 | 1.03 | 15 | -2374.39 | 0.73 | 6887.46 | 46 | 48577.17 | 2.52 |
| 10 | CALL|Grid|Rsi=50-75|Gap=5|Pcr=0.7|Atr=0.02 | no | 33 | 552.52 | 1.03 | 15 | -2374.39 | 0.73 | 6887.46 | 46 | 48577.17 | 2.52 |

## PUT NIFTY

Baseline: trades 17, net 15261.76, PF 1.97, validation net -6854.41 (PF 0, 4 trades), test net 12545.68 (PF 2.42, 10 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 694 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 192, net 81948.58, PF 1.52, validation net 14897.43 (PF 1.46, 43 trades), test net 33853.53 (PF 1.93, 43 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 36099.97, PF 1.44; held-out test net 50898.83, PF 1.44 on 124 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 292 | 58910.47 | 1.25 | 103 | 36099.97 | 1.44 | 20434.39 | 124 | 50898.83 | 1.44 |
| 2 | PUT|Rsi=25-42+Pcr=0.6 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 3 | PUT|Rsi=25-42+Pcr=0.7 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 4 | PUT|Rsi=25-42+Pcr=0.8 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 5 | PUT|Rsi=25-42+Pcr=0.9 | YES | 137 | 14050.92 | 1.11 | 49 | 31517.25 | 2.1 | 8539.48 | 61 | 41650.19 | 1.7 |
| 6 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 137 | 19155.27 | 1.15 | 52 | 29939.64 | 1.89 | 8276.22 | 48 | 44200.32 | 2.11 |
| 7 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 137 | 19155.27 | 1.15 | 52 | 29939.64 | 1.89 | 8276.22 | 48 | 44200.32 | 2.11 |
| 8 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.02 | YES | 137 | 19155.27 | 1.15 | 52 | 29939.64 | 1.89 | 8276.22 | 48 | 44200.32 | 2.11 |
| 9 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.02 | YES | 137 | 19155.27 | 1.15 | 52 | 29939.64 | 1.89 | 8276.22 | 48 | 44200.32 | 2.11 |
| 10 | PUT|Grid|Rsi=25-42|Gap=5|Pcr=0.6|Atr=0.02 | YES | 146 | 19493.94 | 1.15 | 54 | 29159.66 | 1.87 | 9411.19 | 59 | 45749.81 | 1.8 |

## PUT SENSEX

Baseline: trades 98, net 61731.48, PF 2.25, validation net -1111.6 (PF 0, 1 trades), test net 50294.97 (PF 2.04, 86 trades).
**Best validated: PUT|NoPcr+NoAtr** - validation net 14695.74, PF 1.33; held-out test net 62356.94, PF 2.1 on 107 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+NoAtr | YES | 152 | 40075.38 | 1.58 | 96 | 14695.74 | 1.33 | 9353.15 | 107 | 62356.94 | 2.1 |
| 2 | PUT|NoRsi+NoPcr | YES | 154 | 22816.73 | 1.25 | 73 | 10752.28 | 1.27 | 9181.34 | 155 | 71506.33 | 1.7 |
| 3 | PUT|NoEmaGap+NoPcr | YES | 68 | 12746.01 | 1.32 | 48 | 3262.88 | 1.13 | 12646.91 | 93 | 56594.02 | 2.06 |
| 4 | PUT|NoPcr | no | 61 | 21968.27 | 1.75 | 45 | -1532.31 | 0.94 | 12286.07 | 90 | 50890.1 | 1.95 |

## PUT ALL

Baseline: trades 261, net 119462.21, PF 1.63, validation net 6931.42 (PF 1.17, 47 trades), test net 68557.94 (PF 2.06, 101 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 29342.08, PF 1.32; held-out test net 63981.77, PF 1.37 on 205 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 300 | 55530.65 | 1.23 | 111 | 29342.08 | 1.32 | 22633.24 | 205 | 63981.77 | 1.37 |
| 2 | PUT|Rsi=25-42+Pcr=0.9 | YES | 142 | 16360.41 | 1.13 | 52 | 24662.84 | 1.69 | 8673.27 | 118 | 74519.17 | 1.83 |
| 3 | PUT|NoEmaGap+NoAtr | YES | 168 | 62805.27 | 1.59 | 75 | 24098.96 | 1.42 | 11321.48 | 128 | 82190.53 | 2.1 |
| 4 | PUT|Rsi=25-42+Pcr=0.7 | YES | 145 | 19186.99 | 1.15 | 53 | 23551.24 | 1.64 | 8673.27 | 120 | 80384.9 | 1.92 |
| 5 | PUT|Rsi=25-42+Pcr=0.8 | YES | 144 | 19186.99 | 1.15 | 53 | 23551.24 | 1.64 | 8673.27 | 117 | 78972.22 | 1.89 |
| 6 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.6|Atr=0.02 | YES | 168 | 28328.63 | 1.21 | 61 | 23117.16 | 1.52 | 8562.78 | 137 | 76695.01 | 1.83 |
| 7 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.7|Atr=0.02 | YES | 164 | 28427.48 | 1.22 | 61 | 23117.16 | 1.52 | 8562.78 | 136 | 77864.56 | 1.87 |
| 8 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.8|Atr=0.02 | YES | 157 | 29808.65 | 1.23 | 61 | 23117.16 | 1.52 | 8562.78 | 128 | 73379.73 | 1.82 |
| 9 | PUT|Rsi=25-42+Pcr=0.6 | YES | 146 | 19186.99 | 1.15 | 54 | 22396.31 | 1.59 | 8673.27 | 120 | 80219.36 | 1.89 |
| 10 | PUT|Grid|Rsi=25-40|Gap=5|Pcr=0.9|Atr=0.02 | YES | 149 | 23199.38 | 1.18 | 59 | 22332.57 | 1.52 | 8562.78 | 127 | 67893.54 | 1.76 |

