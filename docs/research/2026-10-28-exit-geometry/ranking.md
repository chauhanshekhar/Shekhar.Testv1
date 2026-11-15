# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 0 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 9 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Exit|S1.00-T3.0 | no | 2 | -2488.83 | 0 | 6 | 352.86 | 1.07 | 5350.63 | 34 | 36701.01 | 1.86 |
| 2 | CALL|Exit|S1.10-T3.0 | no | 2 | -2683.58 | 0 | 6 | -16.32 | 1 | 5885.7 | 34 | 37549.89 | 1.84 |
| 3 | CALL|Exit|S1.00-T2.0 | no | 2 | -2488.83 | 0 | 6 | -42.48 | 0.99 | 5350.63 | 34 | 47988.81 | 2.12 |
| 4 | CALL|Exit|S1.10-T2.0 | no | 2 | -2683.58 | 0 | 6 | -411.66 | 0.93 | 5885.7 | 34 | 44909.96 | 2.01 |
| 5 | CALL|Exit|S1.00-T1.5 | no | 2 | -2488.83 | 0 | 6 | -954.82 | 0.82 | 5350.63 | 34 | 53240.71 | 2.25 |
| 6 | CALL|Exit|S1.10-T1.5 | no | 2 | -2683.58 | 0 | 6 | -1324 | 0.78 | 5885.7 | 34 | 47461.33 | 2.01 |
| 7 | CALL|Exit|S1.25-T3.0 | no | 2 | -3049.52 | 0 | 6 | -1916.64 | 0.71 | 6688.29 | 34 | 31601.15 | 1.63 |
| 8 | CALL|Exit|S1.25-T2.0 | no | 2 | -3049.52 | 0 | 6 | -2265.02 | 0.66 | 6688.29 | 34 | 40397.93 | 1.8 |
| 9 | CALL|Exit|S1.25-T1.5 | no | 2 | -3049.52 | 0 | 6 | -3370.56 | 0.5 | 6688.29 | 34 | 39815.76 | 1.75 |

## CALL SENSEX

**Best validated: CALL|Exit|S1.25-T1.5** - validation net 6182.46, PF 5; held-out test net 33079.83, PF 2.27 on 33 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Exit|S1.25-T1.5 | YES | 5 | 1820.64 | 2.56 | 4 | 6182.46 | 5 | 1543.8 | 33 | 33079.83 | 2.27 |
| 2 | CALL|Exit|S1.10-T1.5 | YES | 5 | 1028.77 | 1.46 | 4 | 5697.66 | 5.19 | 1358.54 | 33 | 31272.03 | 2.27 |
| 3 | CALL|Exit|S1.00-T1.5 | YES | 5 | 1186.31 | 1.58 | 4 | 5374.46 | 5.35 | 1235.04 | 34 | 31630.34 | 2.34 |
| 4 | CALL|Exit|S1.25-T2.0 | no | 5 | -2327.61 | 0 | 4 | 6210.57 | 5.02 | 1543.8 | 32 | 38751.14 | 2.49 |
| 5 | CALL|Exit|S1.10-T2.0 | no | 5 | -3256.24 | 0 | 4 | 5832.89 | 5.29 | 1358.54 | 32 | 37413.06 | 2.52 |
| 6 | CALL|Exit|S1.00-T2.0 | no | 5 | -2960.22 | 0 | 4 | 5581.11 | 5.52 | 1235.04 | 34 | 36518.96 | 2.49 |
| 7 | CALL|Exit|S1.25-T3.0 | no | 5 | -2327.61 | 0 | 4 | 5524.7 | 4.58 | 1543.8 | 32 | 48843.96 | 2.88 |
| 8 | CALL|Exit|S1.10-T3.0 | no | 5 | -3256.24 | 0 | 4 | 5254.13 | 4.87 | 1358.54 | 32 | 44262.35 | 2.8 |
| 9 | CALL|Exit|S1.00-T3.0 | no | 5 | -2960.22 | 0 | 4 | 5073.76 | 5.11 | 1235.04 | 34 | 42534.04 | 2.73 |

## CALL ALL

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 9 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Exit|S1.25-T1.5 | no | 7 | -1228.88 | 0.71 | 11 | 7044.23 | 1.86 | 6688.29 | 54 | 30251.04 | 1.36 |
| 2 | CALL|Exit|S1.25-T3.0 | no | 7 | -5377.13 | 0 | 11 | 6857.55 | 1.83 | 6688.29 | 53 | 40131.44 | 1.51 |
| 3 | CALL|Exit|S1.00-T2.0 | no | 7 | -5449.05 | 0 | 11 | 6792.14 | 2.03 | 5350.63 | 59 | 50640.14 | 1.73 |
| 4 | CALL|Exit|S1.00-T3.0 | no | 7 | -5449.05 | 0 | 11 | 6639.47 | 2.01 | 5350.63 | 58 | 46368.67 | 1.68 |
| 5 | CALL|Exit|S1.10-T2.0 | no | 7 | -5939.82 | 0 | 11 | 6440.67 | 1.89 | 5885.7 | 57 | 49167.05 | 1.67 |
| 6 | CALL|Exit|S1.10-T3.0 | no | 7 | -5939.82 | 0 | 11 | 6407.85 | 1.88 | 5885.7 | 55 | 45196.5 | 1.62 |
| 7 | CALL|Exit|S1.00-T1.5 | no | 7 | -1302.52 | 0.71 | 11 | 5383 | 1.82 | 5350.63 | 59 | 45746.27 | 1.66 |
| 8 | CALL|Exit|S1.25-T2.0 | no | 7 | -5377.13 | 0 | 11 | 5326.62 | 1.65 | 6688.29 | 55 | 37623.84 | 1.47 |
| 9 | CALL|Exit|S1.10-T1.5 | no | 7 | -1654.82 | 0.66 | 11 | 5083.1 | 1.7 | 5885.7 | 55 | 40258.7 | 1.53 |

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 9 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Exit|S1.00-T1.5 | no | 4 | 5570.68 | 3.46 | 3 | -4538.18 | 0 | 4538.18 | 23 | 19863.72 | 1.75 |
| 2 | PUT|Exit|S1.00-T2.0 | no | 4 | 3396.14 | 2.5 | 3 | -4538.18 | 0 | 4538.18 | 23 | 19911.29 | 1.75 |
| 3 | PUT|Exit|S1.00-T3.0 | no | 4 | 4530.68 | 3 | 3 | -4538.18 | 0 | 4538.18 | 23 | 23026.19 | 1.87 |
| 4 | PUT|Exit|S1.10-T1.5 | no | 4 | 5305.76 | 3.34 | 3 | -4538.18 | 0 | 4538.18 | 23 | 18944.91 | 1.7 |
| 5 | PUT|Exit|S1.10-T2.0 | no | 4 | 3143.16 | 2.39 | 3 | -4538.18 | 0 | 4538.18 | 23 | 19401.99 | 1.72 |
| 6 | PUT|Exit|S1.10-T3.0 | no | 4 | 4174.56 | 2.84 | 3 | -4538.18 | 0 | 4538.18 | 23 | 21288.3 | 1.79 |
| 7 | PUT|Exit|S1.25-T1.5 | no | 4 | 7540.4 | 4.32 | 3 | -4804.25 | 0 | 4804.25 | 23 | 15287.04 | 1.55 |
| 8 | PUT|Exit|S1.25-T2.0 | no | 4 | 2927.63 | 2.29 | 3 | -4804.25 | 0 | 4804.25 | 23 | 14013.14 | 1.5 |
| 9 | PUT|Exit|S1.25-T3.0 | no | 4 | 3861.6 | 2.7 | 3 | -4804.25 | 0 | 4804.25 | 23 | 16838.46 | 1.6 |

## PUT BANKNIFTY

**Best validated: PUT|Exit|S1.00-T3.0** - validation net 19416.93, PF 2.59; held-out test net 33212.02, PF 1.46 on 74 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Exit|S1.00-T3.0 | YES | 113 | 31747.88 | 1.36 | 23 | 19416.93 | 2.59 | 6744.87 | 74 | 33212.02 | 1.46 |
| 2 | PUT|Exit|S1.10-T2.0 | YES | 126 | 32831.47 | 1.32 | 28 | 18600.45 | 2.18 | 7434.99 | 74 | 18162.81 | 1.23 |
| 3 | PUT|Exit|S1.10-T3.0 | YES | 105 | 36728.12 | 1.44 | 22 | 18539.66 | 2.59 | 5759.47 | 71 | 33383.96 | 1.47 |
| 4 | PUT|Exit|S1.00-T2.0 | YES | 130 | 31289.49 | 1.3 | 30 | 17613.94 | 2.02 | 8268.06 | 78 | 17817.36 | 1.23 |
| 5 | PUT|Exit|S1.00-T1.5 | YES | 132 | 33901.42 | 1.34 | 30 | 16354.53 | 1.92 | 8268.06 | 79 | 15833.03 | 1.2 |
| 6 | PUT|Exit|S1.10-T1.5 | YES | 126 | 43767.21 | 1.45 | 29 | 15852.95 | 1.9 | 7434.99 | 75 | 14688.36 | 1.19 |
| 7 | PUT|Exit|S1.25-T2.0 | YES | 118 | 28556.43 | 1.26 | 28 | 15682 | 1.88 | 8448.85 | 72 | 23455.53 | 1.28 |
| 8 | PUT|Exit|S1.25-T3.0 | YES | 101 | 26026.73 | 1.28 | 24 | 14575.01 | 1.91 | 6544.85 | 69 | 33204.47 | 1.44 |
| 9 | PUT|Exit|S1.25-T1.5 | YES | 120 | 35459.84 | 1.34 | 29 | 13949.18 | 1.7 | 8448.85 | 72 | 19565.72 | 1.25 |

## PUT SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 9 of 9 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Exit|S1.10-T2.0 | no | 11 | 15539.32 | NULL | 5 | 1484.17 | 1.5 | 1985.45 | 96 | 55033.64 | 1.92 |
| 2 | PUT|Exit|S1.25-T2.0 | no | 11 | 15882.92 | NULL | 5 | 1358.58 | 1.4 | 2256.19 | 93 | 53302.88 | 1.84 |
| 3 | PUT|Exit|S1.10-T1.5 | no | 11 | 12797.6 | NULL | 5 | 1272.5 | 1.43 | 1985.45 | 101 | 54727.55 | 1.92 |
| 4 | PUT|Exit|S1.25-T1.5 | no | 11 | 12679.65 | NULL | 5 | 1146.91 | 1.34 | 2256.19 | 98 | 56082.77 | 1.91 |
| 5 | PUT|Exit|S1.10-T3.0 | no | 11 | 14598.41 | NULL | 5 | 976.3 | 1.33 | 2956.92 | 94 | 67823.68 | 2.19 |
| 6 | PUT|Exit|S1.25-T3.0 | no | 11 | 14692.59 | NULL | 5 | 920.12 | 1.27 | 3360.17 | 90 | 71269.9 | 2.27 |
| 7 | PUT|Exit|S1.00-T2.0 | no | 11 | 15310.25 | NULL | 6 | 641.65 | 1.18 | 2731.19 | 98 | 57130.21 | 2.01 |
| 8 | PUT|Exit|S1.00-T1.5 | no | 11 | 12876.23 | NULL | 6 | 429.99 | 1.12 | 2731.19 | 103 | 57475.15 | 2.01 |
| 9 | PUT|Exit|S1.00-T3.0 | no | 11 | 14560.24 | NULL | 6 | 87.51 | 1.02 | 3614.33 | 96 | 69938.68 | 2.27 |

## PUT ALL

**Best validated: PUT|Exit|S1.10-T2.0** - validation net 15546.44, PF 1.67; held-out test net 52863.96, PF 1.43 on 134 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Exit|S1.10-T2.0 | YES | 129 | 42138.69 | 1.44 | 35 | 15546.44 | 1.67 | 7434.99 | 134 | 52863.96 | 1.43 |
| 2 | PUT|Exit|S1.10-T3.0 | YES | 112 | 46326.77 | 1.57 | 29 | 14977.79 | 1.78 | 5759.47 | 128 | 50449 | 1.43 |
| 3 | PUT|Exit|S1.00-T3.0 | YES | 119 | 44587.29 | 1.53 | 31 | 14966.27 | 1.74 | 6744.87 | 134 | 65841.32 | 1.58 |
| 4 | PUT|Exit|S1.00-T2.0 | YES | 134 | 42135.47 | 1.44 | 38 | 13717.42 | 1.54 | 8268.06 | 138 | 60467.86 | 1.51 |
| 5 | PUT|Exit|S1.10-T1.5 | YES | 132 | 50685.77 | 1.54 | 36 | 12587.28 | 1.5 | 7434.99 | 137 | 55216.69 | 1.48 |
| 6 | PUT|Exit|S1.00-T1.5 | YES | 137 | 42658.05 | 1.45 | 38 | 12246.34 | 1.47 | 8268.06 | 142 | 65405.28 | 1.58 |
| 7 | PUT|Exit|S1.25-T2.0 | YES | 125 | 36767.01 | 1.36 | 35 | 12236.33 | 1.47 | 8448.85 | 132 | 46528.17 | 1.36 |
| 8 | PUT|Exit|S1.25-T3.0 | YES | 110 | 35094.31 | 1.39 | 31 | 10690.88 | 1.44 | 6544.85 | 127 | 40006.49 | 1.33 |
| 9 | PUT|Exit|S1.25-T1.5 | YES | 129 | 42701.31 | 1.42 | 36 | 10291.84 | 1.37 | 8448.85 | 135 | 53890.4 | 1.44 |

