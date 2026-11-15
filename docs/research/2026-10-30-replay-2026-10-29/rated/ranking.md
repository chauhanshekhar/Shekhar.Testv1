# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 37 of 37 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|RATED|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 2 | CALL|RATED|S50-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 3 | CALL|RATED|S50-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 4 | CALL|RATED|S50-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 5 | CALL|RATED|S50-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 6 | CALL|RATED|S55-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 7 | CALL|RATED|S55-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 8 | CALL|RATED|S55-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 9 | CALL|RATED|S55-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |
| 10 | CALL|RATED|S60-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 1 | 3306.89 | NULL |

## CALL BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 37 of 37 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|RATED|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 2 | CALL|RATED|S50-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 9490.16 | 2.68 |
| 3 | CALL|RATED|S50-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 4920.02 | 1.64 |
| 4 | CALL|RATED|S50-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 4920.02 | 1.64 |
| 5 | CALL|RATED|S50-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 4920.02 | 1.64 |
| 6 | CALL|RATED|S55-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 9490.16 | 2.68 |
| 7 | CALL|RATED|S55-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 4920.02 | 1.64 |
| 8 | CALL|RATED|S55-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 4920.02 | 1.64 |
| 9 | CALL|RATED|S55-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 4920.02 | 1.64 |
| 10 | CALL|RATED|S60-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 8 | 4920.02 | 1.64 |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 37 of 37 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|RATED|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 2 | CALL|RATED|S50-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6422.94 | 2.07 |
| 3 | CALL|RATED|S50-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6422.94 | 2.07 |
| 4 | CALL|RATED|S50-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6422.94 | 2.07 |
| 5 | CALL|RATED|S50-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6212.85 | 2.04 |
| 6 | CALL|RATED|S55-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6422.94 | 2.07 |
| 7 | CALL|RATED|S55-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6422.94 | 2.07 |
| 8 | CALL|RATED|S55-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6422.94 | 2.07 |
| 9 | CALL|RATED|S55-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6212.85 | 2.04 |
| 10 | CALL|RATED|S60-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 10 | 6474.08 | 2.08 |

## CALL ALL

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 37 of 37 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|RATED|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 2 | CALL|RATED|S50-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 12 | 4770.15 | 1.45 |
| 3 | CALL|RATED|S50-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 12 | 4770.15 | 1.45 |
| 4 | CALL|RATED|S50-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 12 | 4770.15 | 1.45 |
| 5 | CALL|RATED|S50-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 9 | 6889.18 | 1.89 |
| 6 | CALL|RATED|S55-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 12 | 4770.15 | 1.45 |
| 7 | CALL|RATED|S55-M10 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 12 | 4770.15 | 1.45 |
| 8 | CALL|RATED|S55-M20 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 12 | 4770.15 | 1.45 |
| 9 | CALL|RATED|S55-M30 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 9 | 6889.18 | 1.89 |
| 10 | CALL|RATED|S60-M0 | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 12 | 4770.15 | 1.45 |

