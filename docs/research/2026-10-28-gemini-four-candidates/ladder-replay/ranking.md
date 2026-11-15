# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 8, net 9720.2, PF 2.42, validation net -2269.09 (PF 0, 1 trades), test net 2179.8 (PF 1.48, 4 trades).
**Best validated: PUT|C1|ReplaceNoFloor** - validation net 3996.79, PF 1.29; held-out test net 3577.91, PF NULL on 1 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|C1|ReplaceNoFloor | YES | 8 | 916.89 | 1.1 | 11 | 3996.79 | 1.29 | 6807.26 | 1 | 3577.91 | NULL |
| 2 | PUT|C1|Baseline | no | 3 | 9809.48 | NULL | 1 | -2269.09 | 0 | 2269.09 | 4 | 2179.8 | 1.48 |

## PUT BANKNIFTY

Baseline: trades 45, net 32311.82, PF 1.94, validation net 14788.61 (PF 7.79, 8 trades), test net 0 (PF NULL, 0 trades).
**Best validated: PUT|C1|Baseline** - validation net 14788.61, PF 7.79; held-out test net 0, PF NULL on 0 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|C1|Baseline | YES | 37 | 17523.21 | 1.54 | 8 | 14788.61 | 7.79 | 2178.66 | 0 | 0 | NULL |
| 2 | PUT|C3|Baseline | YES | 25 | 15082.07 | 1.74 | 6 | 5012.52 | 2.43 | 3501 | 15 | 12484.14 | 1.88 |
| 3 | PUT|C3|Replace | no | 4 | 984 | 1.25 | 3 | 6769.19 | NULL | 0 | 3 | -5149.72 | 0 |
| 4 | PUT|C3|ReplaceNoFloor | no | 4 | 984 | 1.25 | 3 | 6769.19 | NULL | 0 | 3 | -5149.72 | 0 |
| 5 | PUT|C1|Replace | no | 3 | 5049 | NULL | 1 | 1449.49 | NULL | 0 | 0 | 0 | NULL |
| 6 | PUT|C1|ReplaceNoFloor | no | 3 | 5049 | NULL | 1 | 1449.49 | NULL | 0 | 0 | 0 | NULL |

## PUT SENSEX

Baseline: trades 15, net 11884.16, PF 2.07, validation net 2270.61 (PF 2.02, 4 trades), test net 6469.95 (PF 1.73, 9 trades).
**Best validated: PUT|C1|Replace** - validation net 640.12, PF 1.1; held-out test net 0, PF NULL on 0 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|C1|Replace | YES | 29 | 5281 | 1.38 | 12 | 640.12 | 1.1 | 2869.04 | 0 | 0 | NULL |
| 2 | PUT|C1|ReplaceNoFloor | YES | 29 | 5281 | 1.38 | 12 | 640.12 | 1.1 | 2869.04 | 0 | 0 | NULL |
| 3 | PUT|C1|Baseline | no | 2 | 3143.6 | NULL | 4 | 2270.61 | 2.02 | 1132.49 | 9 | 6469.95 | 1.73 |

## PUT ALL

Baseline: trades 62, net 39123.13, PF 1.8, validation net 14790.14 (PF 3.21, 12 trades), test net 5927.77 (PF 1.65, 9 trades).
**Best validated: PUT|C1|Baseline** - validation net 14790.14, PF 3.21; held-out test net 5927.77, PF 1.65 on 9 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|C1|Baseline | YES | 41 | 18405.22 | 1.55 | 12 | 14790.14 | 3.21 | 3311.15 | 9 | 5927.77 | 1.65 |
| 2 | PUT|C3|Baseline | YES | 27 | 15350.49 | 1.75 | 7 | 2477.35 | 1.41 | 3501 | 40 | 31696.73 | 2 |
| 3 | PUT|C1|Replace | YES | 31 | 9310.4 | 1.67 | 13 | 2089.62 | 1.32 | 2869.04 | 0 | 0 | NULL |
| 4 | PUT|C1|ReplaceNoFloor | YES | 31 | 20608.35 | 2.65 | 16 | 1871.39 | 1.11 | 6807.07 | 1 | 3577.91 | NULL |
| 5 | PUT|C3|ReplaceNoFloor | no | 4 | 984 | 1.25 | 6 | 10923.56 | NULL | 0 | 19 | 8420.23 | 2.09 |
| 6 | PUT|C3|Replace | no | 4 | 984 | 1.25 | 4 | 8187.92 | NULL | 0 | 19 | 1725.23 | 1.15 |

