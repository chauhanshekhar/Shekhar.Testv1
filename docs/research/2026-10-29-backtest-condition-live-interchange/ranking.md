# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Ignore; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 64 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**Best validated: CALL|MIX|Off-Rsi+EmaGap+EarlyTrendGate** - validation net 18933.94, PF 1.96; held-out test net 5002.29, PF 1.17 on 26 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Off-Rsi+EmaGap+EarlyTrendGate | YES | 49 | 4034.58 | 1.07 | 16 | 18933.94 | 1.96 | 7781.9 | 26 | 5002.29 | 1.17 |
| 2 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+EarlyTrendGate | YES | 49 | 4034.58 | 1.07 | 16 | 18933.94 | 1.96 | 7781.9 | 26 | 5002.29 | 1.17 |
| 3 | CALL|MIX|Off-Rsi+EmaGap+Atr+EarlyTrendGate | YES | 50 | 6406.5 | 1.12 | 16 | 18933.94 | 1.96 | 7781.9 | 26 | 5002.29 | 1.17 |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+EarlyTrendGate | YES | 50 | 6406.5 | 1.12 | 16 | 18933.94 | 1.96 | 7781.9 | 26 | 5002.29 | 1.17 |
| 5 | CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr+EarlyTrendGate | YES | 75 | 256.2 | 1 | 28 | 4445.13 | 1.1 | 15905.25 | 32 | 5901.61 | 1.15 |
| 6 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr+EarlyTrendGate | YES | 75 | 256.2 | 1 | 28 | 4445.13 | 1.1 | 15905.25 | 32 | 5901.61 | 1.15 |
| 7 | CALL|MIX|Off-EmaGap+Pcr | no | 29 | -1893.79 | 0.95 | 20 | 16832.82 | 1.6 | 15491.96 | 13 | -1087.92 | 0.95 |
| 8 | CALL|MIX|Off-OpeningRsi+EmaGap+Pcr | no | 28 | -12205.49 | 0.71 | 20 | 16832.82 | 1.6 | 15491.96 | 13 | -1087.92 | 0.95 |
| 9 | CALL|MIX|Off-EmaGap+Atr+Pcr | no | 29 | -1893.79 | 0.95 | 20 | 16832.82 | 1.6 | 15491.96 | 13 | -1087.92 | 0.95 |
| 10 | CALL|MIX|Off-OpeningRsi+EmaGap+Atr+Pcr | no | 28 | -12205.49 | 0.71 | 20 | 16832.82 | 1.6 | 15491.96 | 13 | -1087.92 | 0.95 |

## CALL SENSEX

**Best validated: CALL|MIX|Off-Rsi+EarlyTrendGate** - validation net 9999.07, PF 1.45; held-out test net 8035.45, PF 1.36 on 28 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Off-Rsi+EarlyTrendGate | YES | 40 | 21298.66 | 1.74 | 19 | 9999.07 | 1.45 | 13120.67 | 28 | 8035.45 | 1.36 |
| 2 | CALL|MIX|Off-Rsi+OpeningRsi+EarlyTrendGate | YES | 40 | 21298.66 | 1.74 | 19 | 9999.07 | 1.45 | 13120.67 | 28 | 8035.45 | 1.36 |
| 3 | CALL|MIX|Off-Rsi+Atr+EarlyTrendGate | YES | 52 | 30616.33 | 1.97 | 19 | 9999.07 | 1.45 | 13120.67 | 29 | 8035.45 | 1.36 |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi+Atr+EarlyTrendGate | YES | 52 | 30616.33 | 1.97 | 19 | 9999.07 | 1.45 | 13120.67 | 29 | 8035.45 | 1.36 |
| 5 | CALL|MIX|Off-Atr+EarlyTrendGate | YES | 41 | 3238.84 | 1.11 | 19 | 7055.04 | 1.28 | 13120.67 | 28 | 3314.83 | 1.14 |
| 6 | CALL|MIX|Off-Rsi+EmaGap+Pcr | YES | 17 | 19335.22 | 2.76 | 25 | 3558.6 | 1.13 | 9470.8 | 12 | 17334.53 | 4.61 |
| 7 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | YES | 17 | 19335.22 | 2.76 | 25 | 3558.6 | 1.13 | 9470.8 | 12 | 17334.53 | 4.61 |
| 8 | CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr | YES | 20 | 18471.96 | 2.43 | 25 | 3558.6 | 1.13 | 9470.8 | 12 | 17334.53 | 4.61 |
| 9 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr | YES | 20 | 18471.96 | 2.43 | 25 | 3558.6 | 1.13 | 9470.8 | 12 | 17334.53 | 4.61 |
| 10 | CALL|MIX|Off-Pcr+EarlyTrendGate | YES | 45 | 1542.8 | 1.04 | 27 | 259.68 | 1.01 | 16998.21 | 45 | 12398.13 | 1.33 |

## CALL ALL

**Best validated: CALL|MIX|Off-Rsi** - validation net 1375.53, PF 1.04; held-out test net -6019.03, PF 0.78 on 15 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Off-Rsi | YES | 16 | 30469.26 | 3.17 | 18 | 1375.53 | 1.04 | 12281.36 | 15 | -6019.03 | 0.78 |
| 2 | CALL|MIX|Off-Rsi+OpeningRsi | YES | 16 | 30469.26 | 3.17 | 18 | 1375.53 | 1.04 | 12281.36 | 15 | -6019.03 | 0.78 |
| 3 | CALL|MIX|Off-Rsi+Atr | YES | 16 | 30469.26 | 3.17 | 18 | 1375.53 | 1.04 | 12281.36 | 15 | -6019.03 | 0.78 |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi+Atr | YES | 16 | 30469.26 | 3.17 | 18 | 1375.53 | 1.04 | 12281.36 | 15 | -6019.03 | 0.78 |
| 5 | CALL|MIX|Off-Rsi+EmaGap | YES | 28 | 19309.08 | 1.58 | 24 | 418.35 | 1.01 | 13337.12 | 15 | 26821.09 | 2.91 |
| 6 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap | YES | 28 | 19309.08 | 1.58 | 24 | 418.35 | 1.01 | 13337.12 | 15 | 26821.09 | 2.91 |
| 7 | CALL|MIX|Off-Rsi+EmaGap+Atr | YES | 28 | 19309.08 | 1.58 | 24 | 418.35 | 1.01 | 13337.12 | 15 | 26821.09 | 2.91 |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr | YES | 28 | 19309.08 | 1.58 | 24 | 418.35 | 1.01 | 13337.12 | 15 | 26821.09 | 2.91 |
| 9 | CALL|MIX|Off-Pcr | no | 18 | 20969.93 | 2.48 | 24 | -61.28 | 1 | 16605.93 | 16 | -6902.84 | 0.76 |
| 10 | CALL|MIX|Off-Rsi+Pcr | no | 20 | 36416.73 | 3.57 | 24 | -61.28 | 1 | 16605.93 | 16 | -6902.84 | 0.76 |

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 32 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**Best validated: PUT|MIX|Off-Rsi+Pcr** - validation net 27919.31, PF 1.54; held-out test net -15057.69, PF 0.63 on 27 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|MIX|Off-Rsi+Pcr | YES | 59 | 2160.96 | 1.04 | 46 | 27919.31 | 1.54 | 7188.86 | 27 | -15057.69 | 0.63 |
| 2 | PUT|MIX|Off-Rsi+OpeningRsi+Pcr | YES | 59 | 2160.96 | 1.04 | 46 | 27919.31 | 1.54 | 7188.86 | 27 | -15057.69 | 0.63 |
| 3 | PUT|MIX|Off-Rsi+Atr+Pcr | YES | 59 | 2160.96 | 1.04 | 46 | 27919.31 | 1.54 | 7188.86 | 27 | -15057.69 | 0.63 |
| 4 | PUT|MIX|Off-Rsi+OpeningRsi+Atr+Pcr | YES | 59 | 2160.96 | 1.04 | 46 | 27919.31 | 1.54 | 7188.86 | 27 | -15057.69 | 0.63 |
| 5 | PUT|MIX|Off-Rsi | YES | 55 | 10190.8 | 1.19 | 38 | 26664.95 | 1.59 | 7188.86 | 26 | -10366.83 | 0.71 |
| 6 | PUT|MIX|Off-Rsi+OpeningRsi | YES | 55 | 10190.8 | 1.19 | 38 | 26664.95 | 1.59 | 7188.86 | 26 | -10366.83 | 0.71 |
| 7 | PUT|MIX|Off-Rsi+Atr | YES | 55 | 10190.8 | 1.19 | 38 | 26664.95 | 1.59 | 7188.86 | 26 | -10366.83 | 0.71 |
| 8 | PUT|MIX|Off-Rsi+OpeningRsi+Atr | YES | 55 | 10190.8 | 1.19 | 38 | 26664.95 | 1.59 | 7188.86 | 26 | -10366.83 | 0.71 |
| 9 | PUT|MIX|Off-EmaGap+Pcr | YES | 41 | 1734.28 | 1.04 | 28 | 16629.13 | 1.57 | 9368.37 | 8 | -9332.1 | 0.27 |
| 10 | PUT|MIX|Off-EmaGap+Atr+Pcr | YES | 41 | 1734.28 | 1.04 | 28 | 16629.13 | 1.57 | 9368.37 | 8 | -9332.1 | 0.27 |

## PUT SENSEX

**Best validated: PUT|MIX|Off-Rsi+Pcr** - validation net 28779.02, PF 1.53; held-out test net 1288.47, PF 1.05 on 31 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|MIX|Off-Rsi+Pcr | YES | 68 | 36939.47 | 1.98 | 77 | 28779.02 | 1.53 | 10086.91 | 31 | 1288.47 | 1.05 |
| 2 | PUT|MIX|Off-Rsi+OpeningRsi+Pcr | YES | 68 | 36939.47 | 1.98 | 77 | 28779.02 | 1.53 | 10086.91 | 31 | 1288.47 | 1.05 |
| 3 | PUT|MIX|Off-Rsi+Atr+Pcr | YES | 79 | 41291.15 | 2.03 | 77 | 28779.02 | 1.53 | 10086.91 | 31 | 1288.47 | 1.05 |
| 4 | PUT|MIX|Off-Rsi+OpeningRsi+Atr+Pcr | YES | 79 | 41291.15 | 2.03 | 77 | 28779.02 | 1.53 | 10086.91 | 31 | 1288.47 | 1.05 |
| 5 | PUT|MIX|Off-OpeningRsi+Pcr | YES | 40 | 18508.31 | 1.8 | 38 | 28313.32 | 2.54 | 3448.37 | 12 | 138.38 | 1.01 |
| 6 | PUT|MIX|Off-OpeningRsi+EmaGap+Pcr | YES | 40 | 18508.31 | 1.8 | 38 | 28313.32 | 2.54 | 3448.37 | 12 | 138.38 | 1.01 |
| 7 | PUT|MIX|Off-OpeningRsi+Atr+Pcr | YES | 43 | 21000.88 | 1.91 | 38 | 28313.32 | 2.54 | 3448.37 | 12 | 138.38 | 1.01 |
| 8 | PUT|MIX|Off-OpeningRsi+EmaGap+Atr+Pcr | YES | 43 | 21000.88 | 1.91 | 38 | 28313.32 | 2.54 | 3448.37 | 12 | 138.38 | 1.01 |
| 9 | PUT|MIX|Off-Rsi | YES | 61 | 32449 | 1.94 | 56 | 26638.77 | 1.68 | 10086.91 | 25 | 7164.27 | 1.38 |
| 10 | PUT|MIX|Off-Rsi+OpeningRsi | YES | 61 | 32449 | 1.94 | 56 | 26638.77 | 1.68 | 10086.91 | 25 | 7164.27 | 1.38 |

## PUT ALL

**Best validated: PUT|MIX|Off-Rsi+EmaGap+Pcr** - validation net 63742.9, PF 1.9; held-out test net -23995.07, PF 0.59 on 41 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|MIX|Off-Rsi+EmaGap+Pcr | YES | 110 | 22897.16 | 1.23 | 82 | 63742.9 | 1.9 | 7037.3 | 41 | -23995.07 | 0.59 |
| 2 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | YES | 110 | 22897.16 | 1.23 | 82 | 63742.9 | 1.9 | 7037.3 | 41 | -23995.07 | 0.59 |
| 3 | PUT|MIX|Off-Rsi+EmaGap+Atr+Pcr | YES | 116 | 26440.12 | 1.27 | 82 | 63742.9 | 1.9 | 7037.3 | 41 | -23995.07 | 0.59 |
| 4 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr | YES | 116 | 26440.12 | 1.27 | 82 | 63742.9 | 1.9 | 7037.3 | 41 | -23995.07 | 0.59 |
| 5 | PUT|MIX|Off-Rsi+EmaGap | YES | 109 | 28605.86 | 1.3 | 72 | 47766.85 | 1.68 | 11120.52 | 38 | -17828.13 | 0.66 |
| 6 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap | YES | 109 | 28605.86 | 1.3 | 72 | 47766.85 | 1.68 | 11120.52 | 38 | -17828.13 | 0.66 |
| 7 | PUT|MIX|Off-Rsi+EmaGap+Atr | YES | 116 | 30215.25 | 1.32 | 72 | 47766.85 | 1.68 | 11120.52 | 38 | -17828.13 | 0.66 |
| 8 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr | YES | 116 | 30215.25 | 1.32 | 72 | 47766.85 | 1.68 | 11120.52 | 38 | -17828.13 | 0.66 |
| 9 | PUT|MIX|Off-Rsi+Pcr | YES | 84 | 23874.11 | 1.34 | 76 | 45598.24 | 1.71 | 9562.73 | 33 | 458.24 | 1.01 |
| 10 | PUT|MIX|Off-Rsi+OpeningRsi+Pcr | YES | 84 | 23874.11 | 1.34 | 76 | 45598.24 | 1.71 | 9562.73 | 33 | 458.24 | 1.01 |

