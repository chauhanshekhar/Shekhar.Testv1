# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**Best validated: CALL|Any@0.30|D1-1000** - validation net 11575.27, PF 3.56; held-out test net 25395.92, PF 2.25 on 20 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Any@0.30|D1-1000 | YES | 2 | 452.74 | 1.2 | 9 | 11575.27 | 3.56 | 2264.89 | 20 | 25395.92 | 2.25 |
| 2 | CALL|Any@0.30|D2-1015 | YES | 2 | 452.74 | 1.2 | 9 | 11575.27 | 3.56 | 2264.89 | 21 | 22712.2 | 1.99 |
| 3 | CALL|Any@0.30|D3-1030 | YES | 2 | 452.74 | 1.2 | 9 | 11575.27 | 3.56 | 2264.89 | 21 | 22712.2 | 1.99 |
| 4 | CALL|GapUp@0.30|D1-1000 | no | 2 | 452.74 | 1.2 | 3 | 5116.75 | NULL | 0 | 4 | 14287.07 | NULL |
| 5 | CALL|GapUp@0.30|D2-1015 | no | 2 | 452.74 | 1.2 | 3 | 5116.75 | NULL | 0 | 5 | 11603.35 | 5.32 |
| 6 | CALL|GapUp@0.30|D3-1030 | no | 2 | 452.74 | 1.2 | 3 | 5116.75 | NULL | 0 | 5 | 11603.35 | 5.32 |

## CALL BANKNIFTY

**Best validated: CALL|GapUp@0.30|D1-1000** - validation net 2423.91, PF 1.9; held-out test net 26438.62, PF NULL on 5 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUp@0.30|D1-1000 | YES | 13 | 8352.31 | 1.67 | 5 | 2423.91 | 1.9 | 2692.13 | 5 | 26438.62 | NULL |
| 2 | CALL|GapUp@0.30|D2-1015 | YES | 16 | 8086.22 | 1.55 | 5 | 2423.91 | 1.9 | 2692.13 | 5 | 26438.62 | NULL |
| 3 | CALL|GapUp@0.30|D3-1030 | YES | 20 | 5275.95 | 1.27 | 5 | 2423.91 | 1.9 | 2692.13 | 7 | 28486.75 | NULL |
| 4 | CALL|Normal@0.30|A-Live | YES | 33 | 1279.63 | 1.04 | 13 | 1256.72 | 1.06 | 11804.31 | 34 | 27723.56 | 1.55 |
| 5 | CALL|Normal@0.30|D1-1000 | YES | 33 | 1279.63 | 1.04 | 13 | 1256.72 | 1.06 | 11804.31 | 34 | 27723.56 | 1.55 |
| 6 | CALL|Normal@0.30|D2-1015 | YES | 33 | 1279.63 | 1.04 | 13 | 1256.72 | 1.06 | 11804.31 | 34 | 27723.56 | 1.55 |
| 7 | CALL|Normal@0.30|D3-1030 | YES | 33 | 1279.63 | 1.04 | 13 | 1256.72 | 1.06 | 11804.31 | 34 | 27723.56 | 1.55 |
| 8 | CALL|GapUp@0.30|A-Live | no | 4 | -952.84 | 0.75 | 3 | 2423.91 | 1.9 | 2692.13 | 2 | 10647.7 | NULL |
| 9 | CALL|Any@0.30|A-Live | no | 65 | 6766.07 | 1.11 | 35 | -1240.06 | 0.97 | 16725.01 | 39 | 46437.64 | 1.92 |
| 10 | CALL|Any@0.30|D1-1000 | no | 74 | 16071.22 | 1.22 | 37 | -1240.06 | 0.97 | 16725.01 | 42 | 62228.56 | 2.23 |

## CALL SENSEX

**Best validated: CALL|Any@0.30|D1-1000** - validation net 20552.34, PF 1.62; held-out test net 14779.65, PF 1.32 on 48 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Any@0.30|D1-1000 | YES | 69 | 8083.65 | 1.19 | 49 | 20552.34 | 1.62 | 7832.44 | 48 | 14779.65 | 1.32 |
| 2 | CALL|Any@0.30|D2-1015 | YES | 75 | 10805.83 | 1.25 | 50 | 19314.25 | 1.56 | 9070.53 | 51 | 16049.37 | 1.33 |
| 3 | CALL|Any@0.30|D3-1030 | YES | 76 | 9595.8 | 1.21 | 50 | 19314.25 | 1.56 | 9070.53 | 53 | 19842.31 | 1.41 |
| 4 | CALL|GapUp@0.30|D1-1000 | YES | 15 | 12371.89 | 3.05 | 24 | 9928.71 | 1.53 | 7575.18 | 13 | 10987.4 | 2.02 |
| 5 | CALL|GapUp@0.30|D2-1015 | YES | 21 | 15094.08 | 3.05 | 25 | 8690.63 | 1.44 | 8813.26 | 16 | 12257.13 | 2 |
| 6 | CALL|GapUp@0.30|D3-1030 | YES | 22 | 13884.05 | 2.62 | 25 | 8690.63 | 1.44 | 8813.26 | 18 | 16050.06 | 2.31 |
| 7 | CALL|GapUp@0.30|A-Live | YES | 5 | 3839.86 | 4.19 | 20 | 5920.06 | 1.34 | 6238.1 | 8 | 14234.27 | 4.57 |
| 8 | CALL|Any@0.30|A-Live | no | 59 | -448.38 | 0.99 | 45 | 16543.69 | 1.52 | 6495.37 | 43 | 18026.51 | 1.45 |
| 9 | CALL|Normal@0.30|A-Live | no | 40 | -2971.69 | 0.88 | 10 | 5204.2 | 1.81 | 3906.76 | 27 | 1999.55 | 1.07 |
| 10 | CALL|Normal@0.30|D1-1000 | no | 40 | -2971.69 | 0.88 | 10 | 5204.2 | 1.81 | 3906.76 | 27 | 1999.55 | 1.07 |

## CALL ALL

**Best validated: CALL|Any@0.30|D1-1000** - validation net 11466.2, PF 1.18; held-out test net 49142.41, PF 1.59 on 64 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|Any@0.30|D1-1000 | YES | 110 | 30086.31 | 1.36 | 72 | 11466.2 | 1.18 | 14709.91 | 64 | 49142.41 | 1.59 |
| 2 | CALL|Any@0.30|D2-1015 | YES | 115 | 39168.98 | 1.48 | 73 | 10228.11 | 1.16 | 14709.91 | 67 | 52745.87 | 1.62 |
| 3 | CALL|Any@0.30|D3-1030 | YES | 119 | 33014.57 | 1.38 | 73 | 10228.11 | 1.16 | 14709.91 | 70 | 56409.89 | 1.66 |
| 4 | CALL|Any@0.30|A-Live | YES | 96 | 17731.63 | 1.24 | 68 | 10085.65 | 1.16 | 14709.91 | 59 | 35695.65 | 1.44 |
| 5 | CALL|Normal@0.30|A-Live | YES | 65 | 3392.85 | 1.07 | 24 | 9612.24 | 1.35 | 10086.85 | 51 | 10325.9 | 1.13 |
| 6 | CALL|Normal@0.30|D1-1000 | YES | 65 | 3392.85 | 1.07 | 24 | 9612.24 | 1.35 | 10086.85 | 51 | 10325.9 | 1.13 |
| 7 | CALL|Normal@0.30|D2-1015 | YES | 65 | 3392.85 | 1.07 | 24 | 9612.24 | 1.35 | 10086.85 | 51 | 10325.9 | 1.13 |
| 8 | CALL|Normal@0.30|D3-1030 | YES | 65 | 3392.85 | 1.07 | 24 | 9612.24 | 1.35 | 10086.85 | 51 | 10325.9 | 1.13 |
| 9 | CALL|GapUp@0.30|D1-1000 | YES | 24 | 17401.51 | 2.2 | 26 | 8378.66 | 1.42 | 7575.18 | 15 | 30671.42 | 3.83 |
| 10 | CALL|GapUp@0.30|D2-1015 | YES | 31 | 23383.01 | 2.61 | 27 | 7140.58 | 1.34 | 8813.26 | 18 | 34274.87 | 3.74 |

