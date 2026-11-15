# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**Best validated: PUT|Base|AllFiltersOff** - validation net 46295.59, PF 1.51; held-out test net 62782.72, PF 1.71 on 105 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 304 | 63201.18 | 1.26 | 119 | 46295.59 | 1.51 | 15312.67 | 105 | 62782.72 | 1.71 |
| 2 | PUT|Base|LiveForReference | no | 51 | 753.03 | 1.01 | 31 | -2690.9 | 0.91 | 7056.53 | 27 | 36399.67 | 3.46 |

## PUT SENSEX

**Best validated: PUT|Base|AllFiltersOff** - validation net 30322.09, PF 1.31; held-out test net 84065.27, PF 1.72 on 203 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 397 | 38224 | 1.18 | 193 | 30322.09 | 1.31 | 9749.21 | 203 | 84065.27 | 1.72 |

## PUT ALL

**Best validated: PUT|Base|AllFiltersOff** - validation net 70941.75, PF 1.36; held-out test net 169665.3, PF 1.76 on 328 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Base|AllFiltersOff | YES | 711 | 116002.11 | 1.25 | 318 | 70941.75 | 1.36 | 17057.52 | 328 | 169665.3 | 1.76 |
| 2 | PUT|Base|LiveForReference | no | 54 | 5561.64 | 1.1 | 35 | -8025.78 | 0.78 | 9737.43 | 57 | 23269.04 | 1.56 |

