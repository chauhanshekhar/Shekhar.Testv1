# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Base|AllFiltersOff | no | 294 | -33118.92 | 0.89 | 82 | -2904.7 | 0.96 | 30366.94 | 164 | 46057.34 | 1.24 |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Base|AllFiltersOff | no | 393 | -3279.28 | 0.99 | 162 | -6482.12 | 0.94 | 17485.42 | 203 | 74237.87 | 1.5 |

## CALL ALL

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Base|AllFiltersOff | no | 690 | -38213.51 | 0.93 | 255 | -10448.4 | 0.95 | 45056.96 | 412 | 177179.02 | 1.44 |

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**Best validated: PUT|Base|AllFiltersOff** - validation net 42030.94, PF 1.63; held-out test net 44737.61, PF 1.2 on 216 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 337 | 70379.41 | 1.26 | 88 | 42030.94 | 1.63 | 15504.84 | 216 | 44737.61 | 1.2 |
| 2 | PUT|Base|LiveForReference | YES | 115 | 29743.7 | 1.3 | 34 | 18426.33 | 1.81 | 8292.2 | 66 | 23392.04 | 1.33 |

## PUT SENSEX

**Best validated: PUT|Base|AllFiltersOff** - validation net 43917.56, PF 1.43; held-out test net 85280.5, PF 1.51 on 251 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 434 | 41548 | 1.18 | 205 | 43917.56 | 1.43 | 9835.19 | 251 | 85280.5 | 1.51 |

## PUT ALL

**Best validated: PUT|Base|AllFiltersOff** - validation net 87506.05, PF 1.5; held-out test net 177058.93, PF 1.39 on 524 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 783 | 122157.93 | 1.24 | 298 | 87506.05 | 1.5 | 18092.66 | 524 | 177058.93 | 1.39 |
| 2 | PUT|Base|LiveForReference | YES | 123 | 38561.36 | 1.4 | 41 | 14752.68 | 1.48 | 8292.2 | 124 | 59744.17 | 1.57 |

