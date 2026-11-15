# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**Best validated: CALL|GapUpGate|GateOffAll** - validation net 586.98, PF 1.13; held-out test net 11603.35, PF 5.32 on 5 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUpGate|GateOffAll | YES | 2 | 452.74 | 1.2 | 5 | 586.98 | 1.13 | 4529.77 | 5 | 11603.35 | 5.32 |

## CALL BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 1 trades in each). 3 of 3 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUpGate|Live | no | 4 | -952.84 | 0.75 | 3 | 2423.91 | 1.9 | 2692.13 | 2 | 10647.7 | NULL |
| 2 | CALL|GapUpGate|GateOffBANKNIFTY | no | 28 | -2575.7 | 0.91 | 8 | -3389.06 | 0.6 | 5957.91 | 22 | 34692.52 | 3.79 |
| 3 | CALL|GapUpGate|GateOffAll | no | 28 | -2575.7 | 0.91 | 8 | -3389.06 | 0.6 | 5957.91 | 22 | 34692.52 | 3.79 |

## CALL SENSEX

**Best validated: CALL|GapUpGate|GateOffAll** - validation net 7074.51, PF 1.24; held-out test net 20340.81, PF 2.02 on 32 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUpGate|GateOffAll | YES | 32 | 10859.44 | 1.63 | 34 | 7074.51 | 1.24 | 8813.26 | 32 | 20340.81 | 2.02 |
| 2 | CALL|GapUpGate|Live | YES | 5 | 3839.86 | 4.19 | 20 | 5920.06 | 1.34 | 6238.1 | 8 | 14234.27 | 4.57 |
| 3 | CALL|GapUpGate|GateOffBANKNIFTY | YES | 5 | 3839.86 | 4.19 | 20 | 5920.06 | 1.34 | 6238.1 | 8 | 14234.27 | 4.57 |

## CALL ALL

**Best validated: CALL|GapUpGate|GateOffAll** - validation net 8710.18, PF 1.35; held-out test net 47200.13, PF 2.67 on 46 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|GapUpGate|GateOffAll | YES | 52 | 6489.69 | 1.17 | 32 | 8710.18 | 1.35 | 8813.26 | 46 | 47200.13 | 2.67 |
| 2 | CALL|GapUpGate|Live | YES | 9 | 2887.02 | 1.58 | 22 | 6998.11 | 1.37 | 6238.1 | 9 | 21737.69 | 6.45 |
| 3 | CALL|GapUpGate|GateOffBANKNIFTY | YES | 33 | 1264.16 | 1.04 | 26 | 3977.97 | 1.18 | 6238.1 | 29 | 46069.43 | 3.81 |

