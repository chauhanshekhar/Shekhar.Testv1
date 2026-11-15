# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**Best validated: CALL|Floor|OffAll** - validation net 7966.62, PF 1.42; held-out test net 15328.95, PF 1.45 on 26 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Floor|OffAll | YES | 16 | 9993.91 | 1.75 | 18 | 7966.62 | 1.42 | 7704.3 | 26 | 15328.95 | 1.45 |
| 2 | CALL|Floor|OffNIFTY | YES | 16 | 9993.91 | 1.75 | 18 | 7966.62 | 1.42 | 7704.3 | 26 | 15328.95 | 1.45 |

## CALL BANKNIFTY

**Best validated: CALL|Floor|Live** - validation net 21600.73, PF 1.48; held-out test net 30529.44, PF 1.7 on 33 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Floor|Live | YES | 70 | 6880.13 | 1.1 | 38 | 21600.73 | 1.48 | 14237.7 | 33 | 30529.44 | 1.7 |
| 2 | CALL|Floor|OffAll | YES | 70 | 6880.13 | 1.1 | 38 | 21600.73 | 1.48 | 14237.7 | 33 | 30529.44 | 1.7 |
| 3 | CALL|Floor|OffNIFTY | YES | 70 | 6880.13 | 1.1 | 38 | 21600.73 | 1.48 | 14237.7 | 33 | 30529.44 | 1.7 |
| 4 | CALL|Floor|OffBANKNIFTY | YES | 70 | 6880.13 | 1.1 | 38 | 21600.73 | 1.48 | 14237.7 | 33 | 30529.44 | 1.7 |
| 5 | CALL|Floor|OffSENSEX | YES | 70 | 6880.13 | 1.1 | 38 | 21600.73 | 1.48 | 14237.7 | 33 | 30529.44 | 1.7 |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 5 of 5 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Floor|Live | no | 61 | -982.85 | 0.98 | 48 | 19234.69 | 1.57 | 6431.72 | 41 | 13357.76 | 1.35 |
| 2 | CALL|Floor|OffAll | no | 61 | -982.85 | 0.98 | 48 | 19234.69 | 1.57 | 6431.72 | 41 | 13357.76 | 1.35 |
| 3 | CALL|Floor|OffNIFTY | no | 61 | -982.85 | 0.98 | 48 | 19234.69 | 1.57 | 6431.72 | 41 | 13357.76 | 1.35 |
| 4 | CALL|Floor|OffBANKNIFTY | no | 61 | -982.85 | 0.98 | 48 | 19234.69 | 1.57 | 6431.72 | 41 | 13357.76 | 1.35 |
| 5 | CALL|Floor|OffSENSEX | no | 61 | -982.85 | 0.98 | 48 | 19234.69 | 1.57 | 6431.72 | 41 | 13357.76 | 1.35 |

## CALL ALL

**Best validated: CALL|Floor|Live** - validation net 34551.12, PF 1.55; held-out test net 18874.67, PF 1.25 on 53 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Floor|Live | YES | 103 | 17218.33 | 1.21 | 70 | 34551.12 | 1.55 | 12223.16 | 53 | 18874.67 | 1.25 |
| 2 | CALL|Floor|OffBANKNIFTY | YES | 103 | 17218.33 | 1.21 | 70 | 34551.12 | 1.55 | 12223.16 | 53 | 18874.67 | 1.25 |
| 3 | CALL|Floor|OffSENSEX | YES | 103 | 17218.33 | 1.21 | 70 | 34551.12 | 1.55 | 12223.16 | 53 | 18874.67 | 1.25 |
| 4 | CALL|Floor|OffAll | YES | 101 | 9254.89 | 1.11 | 67 | 31783.68 | 1.51 | 12211.41 | 53 | 11340.55 | 1.14 |
| 5 | CALL|Floor|OffNIFTY | YES | 101 | 9254.89 | 1.11 | 67 | 31783.68 | 1.51 | 12211.41 | 53 | 11340.55 | 1.14 |

## PUT NIFTY

**Best validated: PUT|Floor|OffAll** - validation net 46358.51, PF 1.72; held-out test net -18374.15, PF 0.74 on 47 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Floor|OffAll | YES | 47 | 38868.58 | 2.11 | 67 | 46358.51 | 1.72 | 6923.12 | 47 | -18374.15 | 0.74 |
| 2 | PUT|Floor|OffNIFTY | YES | 47 | 38868.58 | 2.11 | 67 | 46358.51 | 1.72 | 6923.12 | 47 | -18374.15 | 0.74 |

## PUT BANKNIFTY

**Best validated: PUT|Floor|Live** - validation net 38140.24, PF 2; held-out test net -10142.61, PF 0.88 on 58 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Floor|Live | YES | 134 | 34752.55 | 1.3 | 49 | 38140.24 | 2 | 10386.67 | 58 | -10142.61 | 0.88 |
| 2 | PUT|Floor|OffAll | YES | 134 | 34752.55 | 1.3 | 49 | 38140.24 | 2 | 10386.67 | 58 | -10142.61 | 0.88 |
| 3 | PUT|Floor|OffNIFTY | YES | 134 | 34752.55 | 1.3 | 49 | 38140.24 | 2 | 10386.67 | 58 | -10142.61 | 0.88 |
| 4 | PUT|Floor|OffBANKNIFTY | YES | 134 | 34752.55 | 1.3 | 49 | 38140.24 | 2 | 10386.67 | 58 | -10142.61 | 0.88 |
| 5 | PUT|Floor|OffSENSEX | YES | 134 | 34752.55 | 1.3 | 49 | 38140.24 | 2 | 10386.67 | 58 | -10142.61 | 0.88 |

## PUT SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 5 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

**Best validated: PUT|Floor|OffAll** - validation net 81881.19, PF 2.02; held-out test net 1605.17, PF 1.01 on 107 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Floor|OffAll | YES | 143 | 27775.53 | 1.23 | 103 | 81881.19 | 2.02 | 8471.87 | 107 | 1605.17 | 1.01 |
| 2 | PUT|Floor|OffNIFTY | YES | 143 | 27775.53 | 1.23 | 103 | 81881.19 | 2.02 | 8471.87 | 107 | 1605.17 | 1.01 |
| 3 | PUT|Floor|Live | YES | 142 | 40925.63 | 1.36 | 91 | 65295.96 | 2.07 | 8218.62 | 107 | 2806.97 | 1.02 |
| 4 | PUT|Floor|OffBANKNIFTY | YES | 142 | 40925.63 | 1.36 | 91 | 65295.96 | 2.07 | 8218.62 | 107 | 2806.97 | 1.02 |
| 5 | PUT|Floor|OffSENSEX | YES | 142 | 40925.63 | 1.36 | 91 | 65295.96 | 2.07 | 8218.62 | 107 | 2806.97 | 1.02 |

