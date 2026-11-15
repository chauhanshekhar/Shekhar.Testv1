# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs NOT modelled (NetProfitAfterCosts = NULL); settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 16 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 8 of 16 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+B:GateOff(RSI 45-65) | no | 124 | -2084.62 | 0.98 | 38 | -12712.94 | 0.72 | 18882.26 | 86 | 42121.25 | 1.48 |
| 2 | CALL|NoPcr+D:Combined(gate OFF, RSI 45-70) | no | 124 | -2084.62 | 0.98 | 38 | -12712.94 | 0.72 | 18882.26 | 86 | 42121.25 | 1.48 |
| 3 | CALL|NoPcr+B:GateOff(RSI 45-65) | no | 124 | -2084.62 | 0.98 | 38 | -12712.94 | 0.72 | 18882.26 | 86 | 42121.25 | 1.48 |
| 4 | CALL|NoPcr+D:Combined(gate OFF, RSI 45-70) | no | 124 | -2084.62 | 0.98 | 38 | -12712.94 | 0.72 | 18882.26 | 86 | 42121.25 | 1.48 |
| 5 | CALL|NoPcr+A:Baseline(gate ON, RSI 45-65) | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 6 | CALL|NoPcr+C:Rsi70(gate ON) | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 7 | CALL|NoPcr+A:Baseline(gate ON, RSI 45-65) | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |
| 8 | CALL|NoPcr+C:Rsi70(gate ON) | no | 46 | 7489.43 | 1.17 | 24 | -16620.27 | 0.43 | 16620.17 | 20 | 27132.59 | 2.34 |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 16 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL ALL

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 8 of 16 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+A:Baseline(gate ON, RSI 45-65) | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 2 | CALL|NoPcr+C:Rsi70(gate ON) | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 3 | CALL|NoPcr+A:Baseline(gate ON, RSI 45-65) | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 4 | CALL|NoPcr+C:Rsi70(gate ON) | no | 63 | 7611 | 1.14 | 36 | -3123.11 | 0.91 | 11319.54 | 31 | 39288.48 | 2.49 |
| 5 | CALL|NoPcr+B:GateOff(RSI 45-65) | no | 135 | 6168.25 | 1.05 | 48 | -5542.5 | 0.89 | 14246.69 | 94 | 56906.02 | 1.63 |
| 6 | CALL|NoPcr+D:Combined(gate OFF, RSI 45-70) | no | 135 | 6168.25 | 1.05 | 48 | -5542.5 | 0.89 | 14246.69 | 94 | 56906.02 | 1.63 |
| 7 | CALL|NoPcr+B:GateOff(RSI 45-65) | no | 135 | 6168.25 | 1.05 | 48 | -5542.5 | 0.89 | 14246.69 | 94 | 56906.02 | 1.63 |
| 8 | CALL|NoPcr+D:Combined(gate OFF, RSI 45-70) | no | 135 | 6168.25 | 1.05 | 48 | -5542.5 | 0.89 | 14246.69 | 94 | 56906.02 | 1.63 |

