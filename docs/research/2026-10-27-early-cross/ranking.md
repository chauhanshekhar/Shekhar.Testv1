# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 0, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 0 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 16 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 20, net 9081.95, PF 1.33, validation net -11108.68 (PF 0, 5 trades), test net 20190.63 (PF 2.2, 15 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 8 of 16 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|EC|Bars5 | no | 97 | -32137.03 | 0.71 | 22 | -1859.43 | 0.92 | 14212.29 | 49 | 13581.57 | 1.2 |
| 2 | CALL|EC|Bars5 | no | 97 | -32137.03 | 0.71 | 22 | -1859.43 | 0.92 | 14212.29 | 49 | 13581.57 | 1.2 |
| 3 | CALL|EC|Bars10+5mNotUp | no | 72 | -6012.34 | 0.92 | 22 | -2101.57 | 0.91 | 13756.75 | 38 | 16531.84 | 1.31 |
| 4 | CALL|EC|Bars10+5mNotUp | no | 72 | -6012.34 | 0.92 | 22 | -2101.57 | 0.91 | 13756.75 | 38 | 16531.84 | 1.31 |
| 5 | CALL|EC|Bars10 | no | 115 | -18832.74 | 0.85 | 25 | -6764.78 | 0.75 | 18419.96 | 54 | 13804.73 | 1.2 |
| 6 | CALL|EC|Bars10 | no | 115 | -18832.74 | 0.85 | 25 | -6764.78 | 0.75 | 18419.96 | 54 | 13804.73 | 1.2 |
| 7 | CALL|EC|Bars15 | no | 127 | -24490.16 | 0.82 | 31 | -11569.32 | 0.64 | 23177.14 | 62 | 15974.84 | 1.19 |
| 8 | CALL|EC|Bars15 | no | 127 | -24490.16 | 0.82 | 31 | -11569.32 | 0.64 | 23177.14 | 62 | 15974.84 | 1.19 |

## CALL SENSEX

Baseline: trades 11, net 22814.64, PF 6.57, validation net 5582.1 (PF NULL, 2 trades), test net 18402.65 (PF 7.29, 8 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 8 of 16 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|EC|Bars5 | no | 45 | 10124.06 | 1.32 | 22 | -2185.96 | 0.88 | 8061.09 | 32 | 3996.6 | 1.17 |
| 2 | CALL|EC|Bars5 | no | 45 | 10124.06 | 1.32 | 22 | -2185.96 | 0.88 | 8061.09 | 32 | 3996.6 | 1.17 |
| 3 | CALL|EC|Bars10 | no | 58 | 11881.19 | 1.3 | 26 | -2570.35 | 0.89 | 9706.25 | 41 | 9025.85 | 1.3 |
| 4 | CALL|EC|Bars10 | no | 58 | 11881.19 | 1.3 | 26 | -2570.35 | 0.89 | 9706.25 | 41 | 9025.85 | 1.3 |
| 5 | CALL|EC|Bars10+5mNotUp | no | 33 | -3735.04 | 0.84 | 24 | -2656.03 | 0.87 | 9791.93 | 20 | 12590.74 | 1.85 |
| 6 | CALL|EC|Bars10+5mNotUp | no | 33 | -3735.04 | 0.84 | 24 | -2656.03 | 0.87 | 9791.93 | 20 | 12590.74 | 1.85 |
| 7 | CALL|EC|Bars15 | no | 68 | 13103.07 | 1.29 | 31 | -5363.21 | 0.79 | 11178.29 | 53 | 8910.99 | 1.23 |
| 8 | CALL|EC|Bars15 | no | 68 | 13103.07 | 1.29 | 31 | -5363.21 | 0.79 | 11178.29 | 53 | 8910.99 | 1.23 |

## CALL ALL

Baseline: trades 28, net 21851.28, PF 1.68, validation net -5526.58 (PF 0.5, 7 trades), test net 28547.97 (PF 2.45, 20 trades).
**Best validated: CALL|EC|Bars10+EmaGap** - validation net 6451.39, PF 1.46; held-out test net 32040.17, PF 1.87 on 37 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|EC|Bars10+EmaGap | YES | 50 | 32455.2 | 2.07 | 18 | 6451.39 | 1.46 | 5474.97 | 37 | 32040.17 | 1.87 |
| 2 | CALL|EC|Bars10+EmaGap | YES | 50 | 32455.2 | 2.07 | 18 | 6451.39 | 1.46 | 5474.97 | 37 | 32040.17 | 1.87 |
| 3 | CALL|EC|Bars10+5mNotUp+EmaGap | YES | 32 | 21399.37 | 2.27 | 17 | 4850.02 | 1.34 | 5474.97 | 24 | 43188.26 | 3.12 |
| 4 | CALL|EC|Bars10+5mNotUp+EmaGap | YES | 32 | 21399.37 | 2.27 | 17 | 4850.02 | 1.34 | 5474.97 | 24 | 43188.26 | 3.12 |
| 5 | CALL|EC|Bars10+Selector | YES | 50 | 30542.87 | 2 | 22 | 1464.6 | 1.07 | 10061.06 | 32 | 41624.39 | 2.45 |
| 6 | CALL|EC|Bars10+Selector | YES | 50 | 30542.87 | 2 | 22 | 1464.6 | 1.07 | 10061.06 | 32 | 41624.39 | 2.45 |
| 7 | CALL|EC|Bars10+5mNotUp | no | 89 | -2617.98 | 0.97 | 45 | -6715.53 | 0.85 | 19182.55 | 48 | 23376.8 | 1.39 |
| 8 | CALL|EC|Bars10+5mNotUp | no | 89 | -2617.98 | 0.97 | 45 | -6715.53 | 0.85 | 19182.55 | 48 | 23376.8 | 1.39 |
| 9 | CALL|EC|Bars5 | no | 121 | -19104.59 | 0.84 | 42 | -6939.52 | 0.84 | 17704.58 | 66 | 36768.67 | 1.56 |
| 10 | CALL|EC|Bars5 | no | 121 | -19104.59 | 0.84 | 42 | -6939.52 | 0.84 | 17704.58 | 66 | 36768.67 | 1.56 |

