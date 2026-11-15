# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 64 of 64 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 2 | CALL|MIX|Off-Rsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 3 | CALL|MIX|Off-OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 5 | CALL|MIX|Off-EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 6 | CALL|MIX|Off-Rsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 7 | CALL|MIX|Off-OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 9 | CALL|MIX|Off-Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 10 | CALL|MIX|Off-Rsi+Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |

## CALL BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 64 of 64 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 2 | CALL|MIX|Off-Rsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 8060.87 | NULL |
| 3 | CALL|MIX|Off-OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 8060.87 | NULL |
| 5 | CALL|MIX|Off-EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 5 | -999.1 | 0.82 |
| 6 | CALL|MIX|Off-Rsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 7 | 7061.76 | 2.26 |
| 7 | CALL|MIX|Off-OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 5 | -999.1 | 0.82 |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 7 | 7061.76 | 2.26 |
| 9 | CALL|MIX|Off-Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 0 | 0 | NULL |
| 10 | CALL|MIX|Off-Rsi+Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 8060.87 | NULL |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 64 of 64 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 2 | CALL|MIX|Off-Rsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 6 | 6872.45 | 5 |
| 3 | CALL|MIX|Off-OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 6 | 6872.45 | 5 |
| 5 | CALL|MIX|Off-EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 3 | 4350.81 | NULL |
| 6 | CALL|MIX|Off-Rsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 7 | 8630.04 | 6.03 |
| 7 | CALL|MIX|Off-OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 3 | 4350.81 | NULL |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 7 | 8630.04 | 6.03 |
| 9 | CALL|MIX|Off-Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 10 | CALL|MIX|Off-Rsi+Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 6 | 6872.45 | 5 |

## CALL ALL

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 0 trades in each). 64 of 64 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Live | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 2 | CALL|MIX|Off-Rsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 4 | 10654.08 | NULL |
| 3 | CALL|MIX|Off-OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 4 | 10654.08 | NULL |
| 5 | CALL|MIX|Off-EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 5 | 2311.3 | 1.64 |
| 6 | CALL|MIX|Off-Rsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 7 | 10372.17 | 3.89 |
| 7 | CALL|MIX|Off-OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 5 | 2311.3 | 1.64 |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 7 | 10372.17 | 3.89 |
| 9 | CALL|MIX|Off-Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 2 | 2593.22 | NULL |
| 10 | CALL|MIX|Off-Rsi+Atr | no | 0 | 0 | NULL | 0 | 0 | NULL | 0 | 4 | 10654.08 | NULL |

