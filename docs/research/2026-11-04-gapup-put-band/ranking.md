# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 0 of 7 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**Best validated: PUT|GapUpBand|Live40-60** - validation net 17721.05, PF 6.98; held-out test net 3565.12, PF 1.27 on 14 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapUpBand|Live40-60 | YES | 28 | 14605.64 | 1.61 | 7 | 17721.05 | 6.98 | 2962.39 | 14 | 3565.12 | 1.27 |
| 2 | PUT|GapUpBand|42-60 | YES | 23 | 8959.62 | 1.46 | 7 | 17721.05 | 6.98 | 2962.39 | 13 | 9260.46 | 1.94 |
| 3 | PUT|GapUpBand|40-65 | YES | 30 | 9693.59 | 1.36 | 7 | 17721.05 | 6.98 | 2962.39 | 14 | 3565.12 | 1.27 |
| 4 | PUT|GapUpBand|45-60 | YES | 18 | 3371.24 | 1.2 | 6 | 15730.79 | 6.31 | 2962.39 | 12 | 7067.33 | 1.72 |
| 5 | PUT|GapUpBand|35-60 | YES | 38 | 30843.63 | 2.15 | 15 | 12736.3 | 1.8 | 5803.28 | 17 | 11936.65 | 1.8 |
| 6 | PUT|GapUpBand|40-55 | no | 28 | 1087.82 | 1.04 | 6 | 17189.65 | NULL | 0 | 14 | 3565.12 | 1.27 |
| 7 | PUT|GapUpBand|45-55 | no | 18 | -10146.58 | 0.53 | 5 | 15199.39 | NULL | 0 | 12 | 7067.33 | 1.72 |

## PUT SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 6 of 7 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapUpBand|40-65 | no | 2 | 266.87 | NULL | 15 | 8695.52 | 2.06 | 2773.11 | 26 | 24582.09 | 2.6 |
| 2 | PUT|GapUpBand|35-60 | no | 2 | 266.87 | NULL | 18 | 8386.78 | 1.81 | 3217.66 | 32 | 32822.4 | 2.96 |
| 3 | PUT|GapUpBand|42-60 | no | 1 | 266.87 | NULL | 14 | 7493.34 | 2.11 | 1712.78 | 20 | 11710.99 | 1.83 |
| 4 | PUT|GapUpBand|45-60 | no | 1 | 266.87 | NULL | 12 | 6490.01 | 2.2 | 1712.78 | 15 | 3211.43 | 1.25 |
| 5 | PUT|GapUpBand|Live40-60 | no | 2 | 266.87 | NULL | 15 | 6074.14 | 1.74 | 2773.11 | 26 | 24582.09 | 2.6 |
| 6 | PUT|GapUpBand|40-55 | no | 1 | 0 | NULL | 15 | 6074.14 | 1.74 | 2773.11 | 24 | 25470.66 | 2.8 |

## PUT ALL

**Best validated: PUT|GapUpBand|Live40-60** - validation net 17459, PF 2.28; held-out test net 28177.17, PF 2.06 on 35 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapUpBand|Live40-60 | YES | 30 | 14872.51 | 1.62 | 19 | 17459 | 2.28 | 5118.83 | 35 | 28177.17 | 2.06 |
| 2 | PUT|GapUpBand|40-65 | YES | 32 | 9960.46 | 1.37 | 19 | 17459 | 2.28 | 5118.83 | 35 | 28177.17 | 2.06 |
| 3 | PUT|GapUpBand|45-60 | YES | 19 | 3638.11 | 1.22 | 14 | 17351.94 | 3.07 | 4168.82 | 23 | 10686.04 | 1.53 |
| 4 | PUT|GapUpBand|40-55 | YES | 29 | 1087.82 | 1.04 | 18 | 16927.6 | 2.58 | 3872.42 | 33 | 29065.74 | 2.15 |
| 5 | PUT|GapUpBand|35-60 | YES | 39 | 30843.63 | 2.15 | 27 | 16433.16 | 1.69 | 6607.25 | 39 | 34162.59 | 2.29 |
| 6 | PUT|GapUpBand|42-60 | YES | 24 | 9226.49 | 1.47 | 17 | 15723.41 | 2.31 | 4168.82 | 29 | 21001.41 | 1.96 |
| 7 | PUT|GapUpBand|45-55 | no | 18 | -10146.58 | 0.53 | 13 | 16820.54 | 4.11 | 1712.78 | 21 | 11574.61 | 1.62 |

