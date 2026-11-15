# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 12, net 20675.34, PF 2.74, validation net 2406.38 (PF NULL, 1 trades), test net 18268.95 (PF 2.54, 11 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 100 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 36, net 31802.61, PF 1.59, validation net 0 (PF NULL, 0 trades), test net 34795.6 (PF 1.68, 34 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 4 of 100 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | no | 51 | 79.82 | 1 | 17 | -6184.53 | 0.65 | 11075.47 | 42 | 40928.83 | 1.69 |
| 2 | CALL|NoPcr | no | 51 | 79.82 | 1 | 17 | -6287.66 | 0.65 | 11178.6 | 42 | 40928.83 | 1.69 |
| 3 | CALL|NoEmaGap+NoPcr | no | 76 | 18456.05 | 1.28 | 22 | -6575.38 | 0.71 | 13287.22 | 51 | 37938.11 | 1.49 |
| 4 | CALL|NoRsi+NoPcr | no | 64 | 6697.68 | 1.11 | 24 | -11109.74 | 0.58 | 16304.71 | 48 | 52781.14 | 1.84 |

## CALL SENSEX

Baseline: trades 25, net 37188.08, PF 4.45, validation net 7710.67 (PF NULL, 3 trades), test net 27659.42 (PF 3.88, 18 trades).
**Best validated: CALL|NoRsi+NoPcr** - validation net 3872.66, PF 1.13; held-out test net 45517.49, PF 2.17 on 47 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoRsi+NoPcr | YES | 58 | 2433.98 | 1.07 | 38 | 3872.66 | 1.13 | 8574.65 | 47 | 45517.49 | 2.17 |
| 2 | CALL|NoPcr | no | 42 | -10649.02 | 0.62 | 33 | 5061.62 | 1.18 | 6615.38 | 29 | 19380.98 | 1.78 |
| 3 | CALL|NoEmaGap+NoPcr | no | 47 | -4796.36 | 0.82 | 37 | 2416.4 | 1.08 | 9331.23 | 30 | 19198.79 | 1.74 |
| 4 | CALL|NoPcr+NoAtr | no | 80 | -3065.55 | 0.93 | 49 | 1663.24 | 1.04 | 13720.38 | 29 | 19380.98 | 1.78 |

## CALL ALL

Baseline: trades 52, net 54119.6, PF 1.85, validation net 7979.11 (PF NULL, 3 trades), test net 47315.49 (PF 1.8, 43 trades).
**Best validated: CALL|NoPcr+NoAtr** - validation net 3532.08, PF 1.08; held-out test net 53980.13, PF 1.8 on 53 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoAtr | YES | 99 | 8950.9 | 1.13 | 53 | 3532.08 | 1.08 | 11389.34 | 53 | 53980.13 | 1.8 |
| 2 | CALL|NoPcr | YES | 75 | 4810.33 | 1.08 | 44 | 483.66 | 1.01 | 12484.37 | 53 | 53980.13 | 1.8 |
| 3 | CALL|NoEmaGap+NoPcr | no | 101 | 26614.2 | 1.36 | 53 | -1732.54 | 0.96 | 14590.83 | 62 | 47853.89 | 1.57 |
| 4 | CALL|NoRsi+NoPcr | no | 90 | 24340.44 | 1.36 | 56 | -8017.12 | 0.85 | 20671.3 | 73 | 56617.54 | 1.65 |

