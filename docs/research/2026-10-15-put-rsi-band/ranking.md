# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## PUT NIFTY

Baseline: trades 15, net -1720.51, PF 0.92, validation net -6904.12 (PF 0, 4 trades), test net -2278.47 (PF 0.8, 7 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 94 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 109, net 34461.8, PF 1.34, validation net -2690.9 (PF 0.91, 31 trades), test net 36399.67 (PF 3.46, 27 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 45176.12, PF 1.68; held-out test net 50345.45, PF 1.74 on 83 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 236 | 65838.39 | 1.36 | 95 | 45176.12 | 1.68 | 10040.4 | 83 | 50345.45 | 1.74 |
| 2 | PUT|Rsi=25-50+Pcr=0.6 | YES | 70 | 20385.49 | 1.31 | 38 | 15579.92 | 1.57 | 7056.53 | 42 | 39437.7 | 2.11 |
| 3 | PUT|Rsi=25-50+Pcr=0.7 | YES | 70 | 20385.49 | 1.31 | 38 | 15579.92 | 1.57 | 7056.53 | 42 | 39437.7 | 2.11 |
| 4 | PUT|Rsi=25-50+Pcr=0.8 | YES | 70 | 20385.49 | 1.31 | 38 | 15579.92 | 1.57 | 7056.53 | 42 | 39437.7 | 2.11 |
| 5 | PUT|Rsi=25-50+Pcr=0.9 | YES | 70 | 20385.49 | 1.31 | 38 | 15579.92 | 1.57 | 7056.53 | 42 | 39437.7 | 2.11 |
| 6 | PUT|Rsi=25-45+Pcr=0.6 | YES | 67 | 25829.67 | 1.43 | 35 | 13942.57 | 1.54 | 7056.53 | 40 | 40794.67 | 2.21 |
| 7 | PUT|Rsi=25-45+Pcr=0.7 | YES | 67 | 25829.67 | 1.43 | 35 | 13942.57 | 1.54 | 7056.53 | 40 | 40794.67 | 2.21 |
| 8 | PUT|Rsi=25-45+Pcr=0.8 | YES | 67 | 25829.67 | 1.43 | 35 | 13942.57 | 1.54 | 7056.53 | 40 | 40794.67 | 2.21 |
| 9 | PUT|Rsi=25-45+Pcr=0.9 | YES | 67 | 25829.67 | 1.43 | 35 | 13942.57 | 1.54 | 7056.53 | 40 | 40794.67 | 2.21 |
| 10 | PUT|Rsi=25-42+Pcr=0.6 | YES | 65 | 12962.31 | 1.21 | 35 | 13617 | 1.53 | 7056.53 | 35 | 39227.72 | 2.34 |

## PUT SENSEX

Baseline: trades 58, net 28738.63, PF 1.86, validation net 3451.5 (PF NULL, 2 trades), test net 17407.16 (PF 1.54, 49 trades).
**Best validated: PUT|NoPcr+NoAtr** - validation net 12408.34, PF 1.48; held-out test net 13273.18, PF 1.29 on 74 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+NoAtr | YES | 107 | 16067.77 | 1.27 | 61 | 12408.34 | 1.48 | 5062.18 | 74 | 13273.18 | 1.29 |
| 2 | PUT|NoRsi+NoPcr | YES | 117 | 6741.33 | 1.09 | 62 | 8863.37 | 1.25 | 8945.56 | 110 | 63136.79 | 2.08 |
| 3 | PUT|NoEmaGap+NoPcr | YES | 47 | 9084.66 | 1.28 | 33 | 7049.55 | 1.59 | 3760.19 | 54 | 21251.65 | 1.67 |
| 4 | PUT|NoPcr | YES | 47 | 9084.66 | 1.28 | 32 | 5515.11 | 1.46 | 4858.88 | 53 | 21251.65 | 1.67 |

## PUT ALL

Baseline: trades 146, net 20804.9, PF 1.16, validation net -8025.78 (PF 0.78, 35 trades), test net 23269.04 (PF 1.56, 57 trades).
**Best validated: PUT|NoRsi+NoEmaGap** - validation net 34521.54, PF 1.45; held-out test net 41322.36, PF 1.39 on 141 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoRsi+NoEmaGap | YES | 244 | 58235.97 | 1.3 | 98 | 34521.54 | 1.45 | 12629.62 | 141 | 41322.36 | 1.39 |
| 2 | PUT|NoPcr+NoAtr | YES | 122 | 18769.25 | 1.21 | 70 | 15368.37 | 1.41 | 6142.27 | 78 | 28051.13 | 1.57 |
| 3 | PUT|NoEmaGap+NoPcr | YES | 110 | 4646.39 | 1.05 | 70 | 7191.96 | 1.15 | 11383.2 | 63 | 20991.2 | 1.46 |
| 4 | PUT|NoPcr | YES | 78 | 9844.94 | 1.14 | 51 | 5800.94 | 1.18 | 6248.65 | 60 | 23222.7 | 1.54 |
| 5 | PUT|Rsi=25-50+Pcr=0.7 | YES | 76 | 14220.89 | 1.2 | 43 | 5389.44 | 1.14 | 8332.92 | 88 | 28906.82 | 1.43 |
| 6 | PUT|Rsi=25-50+Pcr=0.8 | YES | 76 | 12704.17 | 1.18 | 43 | 5389.44 | 1.14 | 8332.92 | 86 | 22819.67 | 1.33 |
| 7 | PUT|NoRsi+NoPcr | YES | 144 | 6864.81 | 1.06 | 77 | 4649.18 | 1.08 | 10372.8 | 113 | 38081.57 | 1.46 |
| 8 | PUT|Rsi=32-50+Pcr=0.6 | YES | 56 | 23634.96 | 1.51 | 38 | 4467.3 | 1.16 | 7382.54 | 71 | 38968.62 | 1.85 |
| 9 | PUT|Rsi=32-50+Pcr=0.7 | YES | 56 | 23634.96 | 1.51 | 37 | 4467.3 | 1.16 | 7382.54 | 71 | 38968.62 | 1.85 |
| 10 | PUT|Rsi=25-50+Pcr=0.6 | YES | 76 | 14220.89 | 1.2 | 45 | 3914.48 | 1.1 | 8332.92 | 88 | 28906.82 | 1.43 |

