# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 7, net 16778.25, PF 8.63, validation net -2198.86 (PF 0, 1 trades), test net 18977.11 (PF NULL, 6 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 36, net 32336.37, PF 1.82, validation net 0 (PF NULL, 0 trades), test net 42076.36 (PF 2.63, 24 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL SENSEX

Baseline: trades 32, net 18234.63, PF 2.08, validation net 2963.99 (PF NULL, 3 trades), test net 15247 (PF 1.97, 26 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

Baseline: trades 59, net 42084.24, PF 1.86, validation net 765.13 (PF 1.35, 4 trades), test net 51035.47 (PF 2.61, 40 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT NIFTY

Baseline: trades 17, net 15261.76, PF 1.97, validation net -6854.41 (PF 0, 4 trades), test net 12545.68 (PF 2.42, 10 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 193, net 81942.58, PF 1.52, validation net 14897.43 (PF 1.46, 43 trades), test net 33847.53 (PF 1.93, 44 trades).
**Best validated: PUT|Breakout|Baseline** - validation net 14897.43, PF 1.46; held-out test net 33847.53, PF 1.93 on 44 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Breakout|Baseline | YES | 106 | 33197.62 | 1.37 | 43 | 14897.43 | 1.46 | 8260.39 | 44 | 33847.53 | 1.93 |
| 2 | PUT|Breakout|Prev5mLevelOn | no | 71 | -1701.47 | 0.98 | 27 | -13887.81 | 0.54 | 15644.98 | 29 | 19900.9 | 1.72 |

## PUT SENSEX

Baseline: trades 98, net 60380.57, PF 2.19, validation net -1111.6 (PF 0, 1 trades), test net 48944.06 (PF 1.99, 86 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 262, net 118105.3, PF 1.62, validation net 6931.42 (PF 1.17, 47 trades), test net 67201.03 (PF 2.02, 102 trades).
**Best validated: PUT|Breakout|Baseline** - validation net 6931.42, PF 1.17; held-out test net 67201.03, PF 2.02 on 102 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Breakout|Baseline | YES | 113 | 43972.85 | 1.53 | 47 | 6931.42 | 1.17 | 8260.39 | 102 | 67201.03 | 2.02 |
| 2 | PUT|Breakout|Prev5mLevelOn | no | 75 | -3603 | 0.95 | 29 | -17358.87 | 0.48 | 18004.44 | 68 | 34786.69 | 1.78 |

