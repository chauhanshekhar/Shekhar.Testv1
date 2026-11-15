# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 37 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**Best validated: CALL|RATED|S55-M0** - validation net 21127.04, PF 1.87; held-out test net 988.41, PF 1.05 on 15 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|RATED|S55-M0 | YES | 28 | 28224.39 | 1.84 | 21 | 21127.04 | 1.87 | 10120.23 | 15 | 988.41 | 1.05 |
| 2 | CALL|RATED|S55-M10 | YES | 28 | 28224.39 | 1.84 | 21 | 21127.04 | 1.87 | 10120.23 | 15 | 988.41 | 1.05 |
| 3 | CALL|RATED|S70-M0 | YES | 25 | 16695.26 | 1.49 | 16 | 20243.14 | 2.1 | 7781.9 | 12 | 4984.35 | 1.34 |
| 4 | CALL|RATED|S70-M10 | YES | 25 | 16695.26 | 1.49 | 16 | 20243.14 | 2.1 | 7781.9 | 12 | 4984.35 | 1.34 |
| 5 | CALL|RATED|S70-M20 | YES | 25 | 16695.26 | 1.49 | 16 | 20243.14 | 2.1 | 7781.9 | 12 | 4984.35 | 1.34 |
| 6 | CALL|RATED|S65-M0 | YES | 28 | 15119.42 | 1.38 | 18 | 17866.6 | 1.86 | 10158.44 | 12 | 4984.35 | 1.34 |
| 7 | CALL|RATED|S65-M10 | YES | 28 | 15119.42 | 1.38 | 18 | 17866.6 | 1.86 | 10158.44 | 12 | 4984.35 | 1.34 |
| 8 | CALL|RATED|S65-M20 | YES | 28 | 14949.77 | 1.37 | 18 | 17866.6 | 1.86 | 10158.44 | 12 | 4984.35 | 1.34 |
| 9 | CALL|RATED|S50-M20 | YES | 28 | 24482.17 | 1.71 | 20 | 17021.65 | 1.7 | 11003.39 | 14 | 2097.13 | 1.12 |
| 10 | CALL|RATED|S55-M20 | YES | 28 | 24482.17 | 1.71 | 20 | 17021.65 | 1.7 | 11003.39 | 14 | 2097.13 | 1.12 |

## CALL SENSEX

**Best validated: CALL|RATED|S55-M20** - validation net 15852.54, PF 1.78; held-out test net 15850.7, PF 3.88 on 12 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|RATED|S55-M20 | YES | 17 | 17365.6 | 2.56 | 23 | 15852.54 | 1.78 | 8033.29 | 12 | 15850.7 | 3.88 |
| 2 | CALL|RATED|S50-M20 | YES | 17 | 17493.46 | 2.58 | 25 | 13477.76 | 1.58 | 11004.07 | 12 | 15850.7 | 3.88 |
| 3 | CALL|RATED|S60-M20 | YES | 17 | 14574.07 | 2.17 | 23 | 12518.4 | 1.61 | 8033.29 | 12 | 15850.7 | 3.88 |
| 4 | CALL|RATED|S55-M0 | YES | 16 | 18545.6 | 2.86 | 24 | 11097.48 | 1.47 | 8033.29 | 12 | 12379.64 | 2.75 |
| 5 | CALL|RATED|S55-M10 | YES | 17 | 17358.66 | 2.55 | 24 | 11097.48 | 1.47 | 8033.29 | 12 | 12379.64 | 2.75 |
| 6 | CALL|RATED|S50-M0 | YES | 18 | 23843.69 | 3.82 | 27 | 9722.92 | 1.38 | 11004.07 | 12 | 20427.9 | 7.02 |
| 7 | CALL|RATED|S50-M10 | YES | 17 | 24868.72 | 4.35 | 26 | 8722.7 | 1.33 | 11004.07 | 12 | 20741.43 | 6.93 |
| 8 | CALL|RATED|S60-M0 | YES | 17 | 14574.07 | 2.17 | 24 | 7763.34 | 1.33 | 8033.29 | 12 | 12379.64 | 2.75 |
| 9 | CALL|RATED|S60-M10 | YES | 17 | 14574.07 | 2.17 | 24 | 7763.34 | 1.33 | 8033.29 | 12 | 12379.64 | 2.75 |

## CALL ALL

**Best validated: CALL|RATED|S65-M30** - validation net 22300.46, PF 2; held-out test net 28239.69, PF 3.44 on 16 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|RATED|S65-M30 | YES | 28 | 24632.87 | 1.66 | 23 | 22300.46 | 2 | 4655.09 | 16 | 28239.69 | 3.44 |
| 2 | CALL|RATED|S50-M30 | YES | 28 | 24632.87 | 1.66 | 24 | 19484.14 | 1.77 | 6550.08 | 16 | 28239.69 | 3.44 |
| 3 | CALL|RATED|S55-M30 | YES | 28 | 24632.87 | 1.66 | 24 | 19484.14 | 1.77 | 6550.08 | 16 | 28239.69 | 3.44 |
| 4 | CALL|RATED|S60-M30 | YES | 28 | 24632.87 | 1.66 | 24 | 19484.14 | 1.77 | 6550.08 | 16 | 28239.69 | 3.44 |
| 5 | CALL|RATED|S70-M30 | YES | 25 | 26378.36 | 1.83 | 23 | 17024.87 | 1.67 | 4655.09 | 16 | 28239.69 | 3.44 |
| 6 | CALL|RATED|S80-M0 | YES | 16 | 16209.53 | 2.15 | 17 | 6278.43 | 1.24 | 7977.88 | 15 | -6019.03 | 0.78 |
| 7 | CALL|RATED|S80-M10 | YES | 16 | 16209.53 | 2.15 | 17 | 6278.43 | 1.24 | 7977.88 | 15 | -6019.03 | 0.78 |
| 8 | CALL|RATED|S80-M20 | YES | 16 | 16209.53 | 2.15 | 17 | 6278.43 | 1.24 | 7977.88 | 15 | -6019.03 | 0.78 |
| 9 | CALL|RATED|S80-M30 | YES | 16 | 16209.53 | 2.15 | 17 | 6278.43 | 1.24 | 7977.88 | 15 | -6019.03 | 0.78 |
| 10 | CALL|RATED|S70-M0 | YES | 27 | 25512.28 | 1.76 | 28 | 4276.91 | 1.12 | 7781.9 | 16 | 28239.69 | 3.44 |

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 37 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**Best validated: PUT|RATED|S50-M0** - validation net 34894.06, PF 1.43; held-out test net -37070.52, PF 0.43 on 44 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|RATED|S50-M0 | YES | 101 | 31304.8 | 1.36 | 76 | 34894.06 | 1.43 | 13295 | 44 | -37070.52 | 0.43 |
| 2 | PUT|RATED|S55-M0 | YES | 101 | 31304.8 | 1.36 | 76 | 34894.06 | 1.43 | 13295 | 44 | -37070.52 | 0.43 |
| 3 | PUT|RATED|S50-M10 | YES | 100 | 35239.63 | 1.43 | 76 | 28706.49 | 1.35 | 13295 | 44 | -37070.52 | 0.43 |
| 4 | PUT|RATED|S50-M20 | YES | 98 | 38977.15 | 1.5 | 76 | 28706.49 | 1.35 | 13295 | 42 | -33564.05 | 0.45 |
| 5 | PUT|RATED|S55-M10 | YES | 100 | 35239.63 | 1.43 | 76 | 28706.49 | 1.35 | 13295 | 44 | -37070.52 | 0.43 |
| 6 | PUT|RATED|S55-M20 | YES | 98 | 38977.15 | 1.5 | 76 | 28706.49 | 1.35 | 13295 | 42 | -33564.05 | 0.45 |
| 7 | PUT|RATED|S60-M0 | YES | 96 | 33306.98 | 1.42 | 74 | 26729.08 | 1.32 | 13295 | 38 | -15973 | 0.68 |
| 8 | PUT|RATED|S60-M10 | YES | 96 | 33306.98 | 1.42 | 74 | 26729.08 | 1.32 | 13295 | 38 | -15973 | 0.68 |
| 9 | PUT|RATED|S60-M20 | YES | 96 | 33306.98 | 1.42 | 74 | 26729.08 | 1.32 | 13295 | 38 | -15973 | 0.68 |
| 10 | PUT|RATED|S50-M30 | YES | 96 | 18091.04 | 1.21 | 75 | 24762.13 | 1.29 | 13396.01 | 41 | -33463.65 | 0.44 |

## PUT SENSEX

**Best validated: PUT|RATED|S75-M0** - validation net 47563.19, PF 1.97; held-out test net 7032.47, PF 1.29 on 30 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|RATED|S75-M0 | YES | 71 | 22910.69 | 1.5 | 76 | 47563.19 | 1.97 | 10035.69 | 30 | 7032.47 | 1.29 |
| 2 | PUT|RATED|S75-M10 | YES | 71 | 22910.69 | 1.5 | 76 | 47563.19 | 1.97 | 10035.69 | 30 | 7032.47 | 1.29 |
| 3 | PUT|RATED|S75-M20 | YES | 71 | 22910.69 | 1.5 | 76 | 47563.19 | 1.97 | 10035.69 | 30 | 7032.47 | 1.29 |
| 4 | PUT|RATED|S75-M30 | YES | 71 | 20119.41 | 1.42 | 76 | 47563.19 | 1.97 | 10035.69 | 30 | 4552.72 | 1.18 |
| 5 | PUT|RATED|S50-M30 | YES | 83 | 19700.19 | 1.35 | 86 | 45077.37 | 1.71 | 10155.82 | 42 | -5492.18 | 0.87 |
| 6 | PUT|RATED|S70-M30 | YES | 76 | 17750.42 | 1.33 | 79 | 42626.29 | 1.8 | 10035.69 | 37 | -5119.36 | 0.87 |
| 7 | PUT|RATED|S70-M0 | YES | 77 | 17593.49 | 1.32 | 80 | 39530.16 | 1.7 | 10035.69 | 38 | -8191.26 | 0.79 |
| 8 | PUT|RATED|S70-M10 | YES | 77 | 17593.49 | 1.32 | 80 | 39530.16 | 1.7 | 10035.69 | 38 | -8191.26 | 0.79 |
| 9 | PUT|RATED|S70-M20 | YES | 77 | 17593.49 | 1.32 | 80 | 39530.16 | 1.7 | 10035.69 | 38 | -8191.26 | 0.79 |
| 10 | PUT|RATED|S55-M30 | YES | 83 | 19851 | 1.35 | 86 | 37085.82 | 1.55 | 15407.27 | 41 | -4715.06 | 0.89 |

## PUT ALL

**Best validated: PUT|RATED|S50-M0** - validation net 78733.86, PF 2.14; held-out test net -24567.14, PF 0.65 on 54 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|RATED|S50-M0 | YES | 118 | 39281.22 | 1.4 | 90 | 78733.86 | 2.14 | 7565.24 | 54 | -24567.14 | 0.65 |
| 2 | PUT|RATED|S50-M20 | YES | 118 | 34646.96 | 1.35 | 90 | 78727.8 | 2.1 | 7565.24 | 52 | -24826.09 | 0.64 |
| 3 | PUT|RATED|S50-M10 | YES | 118 | 39281.22 | 1.4 | 91 | 77344.84 | 2.06 | 7565.24 | 54 | -23860.47 | 0.65 |
| 4 | PUT|RATED|S50-M30 | YES | 114 | 20540.11 | 1.21 | 88 | 73891.47 | 2.09 | 12051.73 | 51 | -19990.71 | 0.7 |
| 5 | PUT|RATED|S55-M20 | YES | 118 | 33314.53 | 1.34 | 90 | 71800.99 | 1.97 | 10482.38 | 51 | -24022.69 | 0.64 |
| 6 | PUT|RATED|S55-M10 | YES | 118 | 37948.79 | 1.39 | 91 | 70360.08 | 1.94 | 10482.38 | 53 | -22868.47 | 0.66 |
| 7 | PUT|RATED|S55-M0 | YES | 118 | 37948.79 | 1.39 | 90 | 69302.02 | 1.91 | 10482.38 | 53 | -22868.47 | 0.66 |
| 8 | PUT|RATED|S55-M30 | YES | 114 | 20540.11 | 1.21 | 88 | 66879.77 | 1.96 | 12051.73 | 50 | -19187.32 | 0.71 |
| 9 | PUT|RATED|S70-M30 | YES | 107 | 9325.92 | 1.09 | 83 | 66248.94 | 1.97 | 10558.65 | 44 | -4484.47 | 0.92 |
| 10 | PUT|RATED|S65-M30 | YES | 111 | 6123.09 | 1.06 | 86 | 65047.1 | 1.92 | 12051.73 | 48 | -12430.4 | 0.78 |

