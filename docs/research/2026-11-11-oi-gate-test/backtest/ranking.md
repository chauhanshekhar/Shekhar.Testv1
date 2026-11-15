# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 3 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|OiGate|OffBANKNIFTY | no | 28 | 34344.39 | 2.06 | 17 | -5173.99 | 0.83 | 19529.95 | 2 | 6960.97 | NULL |
| 2 | CALL|OiGate|Live | no | 25 | 41253.7 | 2.93 | 16 | -11719.33 | 0.62 | 24734.24 | 2 | 7023.38 | NULL |
| 3 | CALL|OiGate|OnAll | no | 25 | 41253.7 | 2.93 | 16 | -11719.33 | 0.62 | 24734.24 | 2 | 7023.38 | NULL |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

**Best validated: CALL|OiGate|OffBANKNIFTY** - validation net 1926.08, PF 1.07; held-out test net 8785.63, PF NULL on 3 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|OiGate|OffBANKNIFTY | YES | 43 | 15691.99 | 1.28 | 19 | 1926.08 | 1.07 | 21411.26 | 3 | 8785.63 | NULL |
| 2 | CALL|OiGate|Live | no | 42 | 20922.48 | 1.41 | 19 | -8247.97 | 0.76 | 26615.56 | 3 | 8848.04 | NULL |
| 3 | CALL|OiGate|OnAll | no | 38 | 32651.25 | 1.8 | 19 | -13344.98 | 0.63 | 30428.25 | 3 | 8848.04 | NULL |

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**Best validated: PUT|OiGate|OffBANKNIFTY** - validation net 3447.98, PF 1.2; held-out test net -8893.8, PF 0.59 on 12 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|OiGate|OffBANKNIFTY | YES | 59 | 3523.46 | 1.05 | 16 | 3447.98 | 1.2 | 6358.7 | 12 | -8893.8 | 0.59 |
| 2 | PUT|OiGate|Live | no | 53 | -127.97 | 1 | 15 | -2327.93 | 0.88 | 10964.24 | 12 | -8930.09 | 0.59 |
| 3 | PUT|OiGate|OnAll | no | 53 | -127.97 | 1 | 15 | -2327.93 | 0.88 | 10964.24 | 12 | -8930.09 | 0.59 |

## PUT SENSEX

**Best validated: PUT|OiGate|OnAll** - validation net 5728.99, PF 1.34; held-out test net -2238.29, PF 0.89 on 18 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|OiGate|OnAll | YES | 55 | 42038.76 | 2.55 | 20 | 5728.99 | 1.34 | 8680.86 | 18 | -2238.29 | 0.89 |
| 2 | PUT|OiGate|Live | YES | 62 | 46023.65 | 2.33 | 22 | 4687.62 | 1.28 | 8823.65 | 18 | -1214.47 | 0.95 |
| 3 | PUT|OiGate|OffBANKNIFTY | YES | 62 | 46023.65 | 2.33 | 22 | 4687.62 | 1.28 | 8823.65 | 18 | -1214.47 | 0.95 |

## PUT ALL

**Best validated: PUT|OiGate|OffBANKNIFTY** - validation net 6713.16, PF 1.26; held-out test net -13564.18, PF 0.67 on 23 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|OiGate|OffBANKNIFTY | YES | 96 | 43813.39 | 1.51 | 28 | 6713.16 | 1.26 | 9576.53 | 23 | -13564.18 | 0.67 |
| 2 | PUT|OiGate|Live | YES | 92 | 38683.05 | 1.47 | 29 | 4079.61 | 1.15 | 10947.8 | 23 | -10129.79 | 0.73 |
| 3 | PUT|OiGate|OnAll | YES | 87 | 34525.58 | 1.46 | 27 | 58.84 | 1 | 16202.72 | 23 | -12582.31 | 0.67 |

