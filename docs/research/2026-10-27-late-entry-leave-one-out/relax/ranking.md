# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 0, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 0 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 26 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 20, net 9081.95, PF 1.33, validation net -11108.68 (PF 0, 5 trades), test net 20190.63 (PF 2.2, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 13 of 26 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+GapTrend:EarlyTrendOnly | no | 69 | -2781.25 | 0.96 | 27 | -11193.44 | 0.64 | 15632.42 | 40 | 35798.93 | 1.95 |
| 2 | CALL|NoPcr+NoEarlyTrendGate | no | 124 | -2084.62 | 0.98 | 38 | -12712.94 | 0.72 | 18882.26 | 87 | 40203.01 | 1.44 |
| 3 | CALL|NoPcr+GapTrend:All | no | 85 | -18937.65 | 0.8 | 32 | -14160.82 | 0.61 | 16037.2 | 52 | 29555 | 1.58 |
| 4 | CALL|NoPcr+Baseline | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 5 | CALL|NoPcr+Sideways5mAgrees | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 6 | CALL|NoPcr+SidewaysOff | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 7 | CALL|NoPcr+RsiCeiling80 | no | 48 | 3125.88 | 1.06 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 8 | CALL|NoPcr+GapTrend:RsiOnly | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 9 | CALL|NoPcr+GapTrend:SidewaysOnly | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 10 | CALL|NoPcr+GapTrend:EmaGapOnly | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |

## CALL SENSEX

Baseline: trades 11, net 22814.64, PF 6.57, validation net 5582.1 (PF NULL, 2 trades), test net 18402.65 (PF 7.29, 8 trades).
**Best validated: CALL|NoPcr+NoEarlyTrendGate** - validation net 18888.86, PF 2.4; held-out test net 38056.3, PF 1.52 on 91 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoEarlyTrendGate | YES | 96 | 5245.85 | 1.08 | 27 | 18888.86 | 2.4 | 5884.69 | 91 | 38056.3 | 1.52 |
| 2 | CALL|NoPcr+GapTrend:All | YES | 60 | 12348.84 | 1.34 | 17 | 13617.66 | 2.79 | 2614.76 | 34 | 12121.9 | 1.4 |
| 3 | CALL|NoPcr+GapTrend:EarlyTrendOnly | YES | 53 | 10067.83 | 1.31 | 17 | 12380.45 | 2.4 | 3809.95 | 32 | 15061.79 | 1.55 |
| 4 | CALL|NoPcr+SidewaysOff | no | 34 | -1394.06 | 0.94 | 19 | 13414.12 | 2.18 | 2867.36 | 21 | 16515.81 | 2.06 |

## CALL ALL

Baseline: trades 28, net 21851.28, PF 1.68, validation net -5526.58 (PF 0.5, 7 trades), test net 28547.97 (PF 2.45, 20 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 13 of 26 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+Baseline | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 2 | CALL|NoPcr+Sideways5mAgrees | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 3 | CALL|NoPcr+RsiCeiling80 | no | 65 | 3247.45 | 1.06 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 4 | CALL|NoPcr+GapTrend:RsiOnly | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 5 | CALL|NoPcr+GapTrend:SidewaysOnly | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 6 | CALL|NoPcr+GapTrend:EmaGapOnly | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 7 | CALL|NoPcr+NoEarlyTrendGate | no | 173 | -10209.38 | 0.94 | 56 | -3522.89 | 0.93 | 14246.69 | 124 | 87129.53 | 1.86 |
| 8 | CALL|NoPcr+GapTrend:EarlyTrendOnly | no | 103 | 9557.57 | 1.11 | 40 | -4992.43 | 0.86 | 11319.54 | 57 | 57945.45 | 2.32 |
| 9 | CALL|NoPcr+NoEmaGap | no | 78 | 34776.36 | 1.66 | 39 | -5714.27 | 0.86 | 12335.19 | 34 | 39767.47 | 2.22 |
| 10 | CALL|NoPcr+GapTrend:All | no | 122 | -2253.81 | 0.98 | 45 | -6722.59 | 0.83 | 11319.54 | 71 | 48761.64 | 1.81 |

## PUT NIFTY

Baseline: trades 21, net 10585.1, PF 1.44, validation net -4804.25 (PF 0, 3 trades), test net 10772.27 (PF 1.63, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 22 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 196, net 47874.19, PF 1.26, validation net 15855.23 (PF 1.95, 27 trades), test net -2478.06 (PF 0.96, 57 trades).
**Best validated: PUT|RsiFloor20** - validation net 18733.04, PF 2; held-out test net 3830.6, PF 1.05 on 62 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|RsiFloor20 | YES | 129 | 27180.69 | 1.22 | 30 | 18733.04 | 2 | 6514.18 | 62 | 3830.6 | 1.05 |
| 2 | PUT|NoPcr+RsiFloor20 | YES | 129 | 27180.69 | 1.22 | 30 | 18733.04 | 2 | 6514.18 | 73 | 9854.62 | 1.12 |
| 3 | PUT|Combined:Sideways5m+Gap5+Rsi | YES | 134 | 21399.65 | 1.17 | 30 | 18653.85 | 1.99 | 6493.37 | 61 | 9280.53 | 1.13 |
| 4 | PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi | YES | 134 | 21399.65 | 1.17 | 30 | 18653.85 | 1.99 | 6493.37 | 72 | 15304.55 | 1.19 |
| 5 | PUT|Baseline | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 6 | PUT|Sideways5mAgrees | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 7 | PUT|SidewaysOff | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 8 | PUT|GapTrend:SidewaysOnly | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 9 | PUT|GapTrend:EmaGapOnly | YES | 113 | 37785.28 | 1.37 | 27 | 15855.23 | 1.95 | 6514.18 | 57 | -2478.06 | 0.96 |
| 10 | PUT|NoPcr+Baseline | YES | 112 | 34497.02 | 1.34 | 27 | 15855.23 | 1.95 | 6514.18 | 67 | 2189.28 | 1.03 |

## PUT SENSEX

Baseline: trades 89, net 47326.82, PF 1.92, validation net 1092.31 (PF 1.33, 5 trades), test net 37417.49 (PF 1.81, 75 trades).
**Best validated: PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi** - validation net 12725.79, PF 1.44; held-out test net 47967.54, PF 1.83 on 93 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi | YES | 84 | 21488.22 | 1.46 | 52 | 12725.79 | 1.44 | 9789.14 | 93 | 47967.54 | 1.83 |
| 2 | PUT|NoPcr+RsiFloor20 | YES | 82 | 24572.18 | 1.56 | 49 | 9894.66 | 1.37 | 7518.15 | 92 | 38887.68 | 1.63 |
| 3 | PUT|NoPcr+GapTrend:All | YES | 70 | 27046.54 | 1.74 | 43 | 4647.98 | 1.2 | 6254.98 | 90 | 46368.6 | 1.75 |
| 4 | PUT|NoPcr+GapTrend:RsiOnly | YES | 70 | 27046.54 | 1.74 | 41 | 4593.75 | 1.2 | 6254.98 | 90 | 46368.6 | 1.75 |
| 5 | PUT|NoPcr+GapTrend:SidewaysOnly | no | 64 | 22139.48 | 1.7 | 31 | -195.92 | 0.99 | 7186.22 | 88 | 38073.51 | 1.62 |
| 6 | PUT|NoPcr+Baseline | no | 64 | 22139.48 | 1.7 | 31 | -250.14 | 0.98 | 7186.22 | 88 | 38073.51 | 1.62 |
| 7 | PUT|NoPcr+Gap5pts | no | 64 | 22139.48 | 1.7 | 31 | -250.14 | 0.98 | 7186.22 | 88 | 38073.51 | 1.62 |
| 8 | PUT|NoPcr+NoEmaGap | no | 64 | 22139.48 | 1.7 | 31 | -250.14 | 0.98 | 7186.22 | 88 | 38073.51 | 1.62 |
| 9 | PUT|NoPcr+GapTrend:EmaGapOnly | no | 64 | 22139.48 | 1.7 | 31 | -250.14 | 0.98 | 7186.22 | 88 | 38073.51 | 1.62 |
| 10 | PUT|NoPcr+SidewaysOff | no | 66 | 24228.1 | 1.77 | 33 | -812.93 | 0.96 | 9849.37 | 90 | 43803.57 | 1.74 |

## PUT ALL

Baseline: trades 261, net 82487.58, PF 1.37, validation net 14412.38 (PF 1.64, 34 trades), test net 37107.91 (PF 1.39, 109 trades).
**Best validated: PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi** - validation net 36807.99, PF 1.97; held-out test net 72767.49, PF 1.69 on 129 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi | YES | 175 | 19262.23 | 1.13 | 73 | 36807.99 | 1.97 | 9588.23 | 129 | 72767.49 | 1.69 |
| 2 | PUT|RsiFloor20 | YES | 136 | 24642.62 | 1.19 | 50 | 31514.41 | 2.09 | 6514.18 | 116 | 38925.33 | 1.36 |
| 3 | PUT|Combined:Sideways5m+Gap5+Rsi | YES | 141 | 18861.58 | 1.14 | 50 | 31489.45 | 2.08 | 6493.37 | 116 | 51889.46 | 1.5 |
| 4 | PUT|NoPcr+RsiFloor20 | YES | 170 | 23706.31 | 1.16 | 70 | 30942.4 | 1.84 | 7630.65 | 129 | 59109.72 | 1.53 |
| 5 | PUT|NoPcr+GapTrend:RsiOnly | YES | 161 | 23134.29 | 1.16 | 60 | 19812.95 | 1.59 | 7630.65 | 125 | 58421.58 | 1.55 |
| 6 | PUT|NoPcr+GapTrend:All | YES | 162 | 30730.14 | 1.22 | 63 | 17757.63 | 1.5 | 7750.82 | 125 | 58421.58 | 1.55 |
| 7 | PUT|NoPcr+GapTrend:SidewaysOnly | YES | 152 | 24591.25 | 1.19 | 50 | 17213.04 | 1.65 | 7630.65 | 123 | 50346.2 | 1.47 |
| 8 | PUT|NoPcr+Baseline | YES | 152 | 24591.25 | 1.19 | 50 | 17158.82 | 1.65 | 7630.65 | 123 | 50346.2 | 1.47 |
| 9 | PUT|NoPcr+GapTrend:EmaGapOnly | YES | 152 | 29416.66 | 1.23 | 50 | 17158.82 | 1.65 | 7630.65 | 123 | 50346.2 | 1.47 |
| 10 | PUT|GapTrend:All | YES | 130 | 39780 | 1.34 | 45 | 17120.73 | 1.58 | 6514.18 | 112 | 42520.81 | 1.43 |

