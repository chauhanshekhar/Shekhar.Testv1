# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

**Best validated: PUT|Base|AllFiltersOff** - validation net 16783.99, PF 2.01; held-out test net 34637.48, PF 1.5 on 55 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 13 | 7927.57 | 1.58 | 18 | 16783.99 | 2.01 | 7519.08 | 55 | 34637.48 | 1.5 |
| 2 | PUT|Base|LiveForReference | no | 5 | 5265.79 | 2.16 | 9 | -2166.85 | 0.81 | 7088.55 | 21 | 12722.77 | 1.52 |

## PUT BANKNIFTY

**Best validated: PUT|Base|AllFiltersOff** - validation net 72764.27, PF 1.96; held-out test net 20271.81, PF 1.08 on 239 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 367 | 78754.78 | 1.26 | 95 | 72764.27 | 1.96 | 11002.34 | 239 | 20271.81 | 1.08 |
| 2 | PUT|Base|LiveForReference | YES | 124 | 38180.5 | 1.36 | 34 | 39539.81 | 2.85 | 4960.69 | 74 | -2944.33 | 0.97 |

## PUT SENSEX

**Best validated: PUT|Base|AllFiltersOff** - validation net 70696.22, PF 1.55; held-out test net 35559.98, PF 1.19 on 255 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 455 | 57318.8 | 1.24 | 234 | 70696.22 | 1.55 | 9795.02 | 255 | 35559.98 | 1.19 |
| 2 | PUT|Base|LiveForReference | no | 11 | 12608.83 | NULL | 29 | 13646.87 | 1.67 | 5832.4 | 93 | 45806.68 | 1.76 |

## PUT ALL

**Best validated: PUT|Base|AllFiltersOff** - validation net 160244.48, PF 1.73; held-out test net 90469.27, PF 1.18 on 549 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 835 | 144001.15 | 1.26 | 347 | 160244.48 | 1.73 | 16358.36 | 549 | 90469.27 | 1.18 |
| 2 | PUT|Base|LiveForReference | YES | 132 | 44521.84 | 1.42 | 58 | 37645.69 | 1.88 | 4960.69 | 131 | 41460.69 | 1.34 |

