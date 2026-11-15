# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 0, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 0 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 72 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 2, net 2530.61, PF NULL, validation net 0 (PF NULL, 0 trades), test net 2530.61 (PF NULL, 2 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 72 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL SENSEX

Baseline: trades 11, net 2236.09, PF 1.3, validation net -2355.75 (PF 0, 3 trades), test net 4591.84 (PF 1.9, 8 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 72 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

Baseline: trades 11, net 3432.88, PF 1.55, validation net -2355.75 (PF 0, 3 trades), test net 5788.63 (PF 2.49, 8 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 72 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT NIFTY

Baseline: trades 2, net -4669.42, PF 0, validation net -2433.42 (PF 0, 1 trades), test net -2236 (PF 0, 1 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 72 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 15, net -13703.07, PF 0.37, validation net -3091.94 (PF 0.59, 8 trades), test net 3452.27 (PF NULL, 2 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 1 of 72 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|Normal@0.50|Baseline | no | 41 | -14029.8 | 0.71 | 24 | -11521.32 | 0.56 | 11663.73 | 24 | 35846.02 | 4.02 |

## PUT SENSEX

Baseline: trades 11, net 949.18, PF 1.12, validation net 0 (PF NULL, 0 trades), test net 949.18 (PF 1.12, 11 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 72 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT ALL

Baseline: trades 26, net -13331.37, PF 0.56, validation net -5525.36 (PF 0.45, 9 trades), test net 6257.37 (PF 2.02, 12 trades).
**Best validated: PUT|GapDown@0.20|Baseline** - validation net 5372.94, PF 1.35; held-out test net -1024.37, PF 0.95 on 16 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|GapDown@0.20|Baseline | YES | 26 | 14244.96 | 1.65 | 15 | 5372.94 | 1.35 | 5856.58 | 16 | -1024.37 | 0.95 |
| 2 | PUT|Normal@0.30|Baseline | no | 29 | 1851.76 | 1.07 | 17 | -11848.19 | 0.4 | 15300.19 | 41 | 45622.21 | 3.18 |
| 3 | PUT|Normal@0.30|Rsi28-38+Gap10+Atr0.03 | no | 29 | 1851.76 | 1.07 | 17 | -11848.19 | 0.4 | 15300.19 | 41 | 45622.21 | 3.18 |
| 4 | PUT|Normal@0.30|Rsi28-38+Body35 | no | 29 | 1851.76 | 1.07 | 17 | -11848.19 | 0.4 | 15300.19 | 41 | 45622.21 | 3.18 |
| 5 | PUT|Normal@0.50|Baseline | no | 43 | -5026.08 | 0.89 | 28 | -14619.7 | 0.52 | 16331.34 | 51 | 18231.09 | 1.47 |

