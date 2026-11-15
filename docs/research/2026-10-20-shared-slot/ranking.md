# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## BOTH NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 1 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## BOTH BANKNIFTY

**Best validated: BOTH|SharedSlot** - validation net 9392.31, PF 1.34; held-out test net 17771.43, PF 1.36 on 48 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | BOTH|SharedSlot | YES | 109 | 32830.58 | 1.34 | 37 | 9392.31 | 1.34 | 6398.87 | 48 | 17771.43 | 1.36 |

## BOTH SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 1 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## BOTH ALL

**Best validated: BOTH|SharedSlot** - validation net 3625.16, PF 1.11; held-out test net 32905.55, PF 1.37 on 110 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | BOTH|SharedSlot | YES | 115 | 30447.09 | 1.3 | 41 | 3625.16 | 1.11 | 6662.67 | 110 | 32905.55 | 1.37 |

