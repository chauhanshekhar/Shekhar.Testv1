# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 17, net 15281.16, PF 1.96, validation net -6968.9 (PF 0, 4 trades), test net 12551.98 (PF 2.4, 10 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 192, net 81948.58, PF 1.52, validation net 14897.43 (PF 1.46, 43 trades), test net 33853.53 (PF 1.93, 43 trades).
**Best validated: PUT|Confirm|Baseline** - validation net 14897.43, PF 1.46; held-out test net 33853.53, PF 1.93 on 43 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Confirm|Baseline | YES | 106 | 33197.62 | 1.37 | 43 | 14897.43 | 1.46 | 8260.39 | 43 | 33853.53 | 1.93 |
| 2 | PUT|Confirm|NextBar | YES | 87 | 23919.99 | 1.31 | 33 | 3878.65 | 1.14 | 5878.9 | 33 | 33326.44 | 2.24 |

## PUT SENSEX

Baseline: trades 96, net 60417.26, PF 2.22, validation net -1111.6 (PF 0, 1 trades), test net 48980.75 (PF 2.01, 84 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 2 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 259, net 118196.76, PF 1.63, validation net 6816.93 (PF 1.17, 47 trades), test net 67279.4 (PF 2.04, 99 trades).
**Best validated: PUT|Confirm|Baseline** - validation net 6816.93, PF 1.17; held-out test net 67279.4, PF 2.04 on 99 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Confirm|Baseline | YES | 113 | 44100.43 | 1.53 | 47 | 6816.93 | 1.17 | 8260.39 | 99 | 67279.4 | 2.04 |
| 2 | PUT|Confirm|NextBar | no | 97 | 18860.34 | 1.24 | 36 | -1381.92 | 0.96 | 8114.9 | 77 | 54259.83 | 2.08 |

