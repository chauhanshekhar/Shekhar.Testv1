# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**Best validated: CALL|Exit|Ladder** - validation net 21600.73, PF 1.48; held-out test net 30529.44, PF 1.7 on 33 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Exit|Ladder | YES | 70 | 6880.13 | 1.1 | 38 | 21600.73 | 1.48 | 14237.7 | 33 | 30529.44 | 1.7 |
| 2 | CALL|Exit|T2Partial | YES | 69 | 2343.84 | 1.04 | 38 | 16415.71 | 1.36 | 13464.03 | 30 | 28202.25 | 1.65 |
| 3 | CALL|Exit|Legacy | no | 76 | -2752.4 | 0.96 | 42 | 6247.56 | 1.11 | 13122.79 | 39 | 43937.03 | 2.17 |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 3 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Exit|Ladder | no | 61 | -982.85 | 0.98 | 48 | 19234.69 | 1.57 | 6431.72 | 41 | 13357.76 | 1.35 |
| 2 | CALL|Exit|T2Partial | no | 61 | -1499.29 | 0.96 | 48 | 12125.71 | 1.36 | 6734.29 | 38 | 20216.95 | 1.63 |
| 3 | CALL|Exit|Legacy | no | 65 | -1523.02 | 0.96 | 55 | 7806.49 | 1.2 | 6615.33 | 48 | 12953.67 | 1.32 |

## CALL ALL

**Best validated: CALL|Exit|Ladder** - validation net 34551.12, PF 1.55; held-out test net 18874.67, PF 1.25 on 53 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Exit|Ladder | YES | 103 | 17218.33 | 1.21 | 70 | 34551.12 | 1.55 | 12223.16 | 53 | 18874.67 | 1.25 |
| 2 | CALL|Exit|T2Partial | YES | 102 | 12196.17 | 1.15 | 69 | 23179.41 | 1.37 | 12109.3 | 48 | 10492.12 | 1.14 |
| 3 | CALL|Exit|Legacy | no | 114 | -1151.82 | 0.99 | 76 | 18977.23 | 1.26 | 13834.86 | 66 | 21502.29 | 1.28 |

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**Best validated: PUT|Exit|T2Partial** - validation net 43434.53, PF 2.28; held-out test net -11949.53, PF 0.85 on 58 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Exit|T2Partial | YES | 129 | 29822.11 | 1.27 | 48 | 43434.53 | 2.28 | 10843.6 | 58 | -11949.53 | 0.85 |
| 2 | PUT|Exit|Legacy | YES | 149 | 16299.92 | 1.12 | 56 | 42614.73 | 2.03 | 10735.46 | 60 | -21516.23 | 0.76 |
| 3 | PUT|Exit|Ladder | YES | 134 | 34752.55 | 1.3 | 49 | 38140.24 | 2 | 10386.67 | 58 | -10142.61 | 0.88 |

## PUT SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

**Best validated: PUT|Exit|T2Partial** - validation net 66115.96, PF 2.12; held-out test net -3970.55, PF 0.97 on 107 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Exit|T2Partial | YES | 138 | 35329.94 | 1.31 | 88 | 66115.96 | 2.12 | 10359.6 | 107 | -3970.55 | 0.97 |
| 2 | PUT|Exit|Ladder | YES | 142 | 40925.63 | 1.36 | 91 | 65295.96 | 2.07 | 8218.62 | 107 | 2806.97 | 1.02 |
| 3 | PUT|Exit|Legacy | YES | 158 | 20740.22 | 1.15 | 102 | 58068.07 | 1.83 | 8218.62 | 117 | -7579.26 | 0.95 |

