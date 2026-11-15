# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector UseStoredSnapshot; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 64 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

**Best validated: CALL|MIX|Off-Rsi+EmaGap+Pcr+EarlyTrendGate** - validation net 9226.64, PF 1.43; held-out test net 15586.83, PF 1.54 on 27 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Off-Rsi+EmaGap+Pcr+EarlyTrendGate | YES | 44 | 11036.56 | 1.23 | 17 | 9226.64 | 1.43 | 12379.29 | 27 | 15586.83 | 1.54 |
| 2 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr+EarlyTrendGate | YES | 44 | 11036.56 | 1.23 | 17 | 9226.64 | 1.43 | 12379.29 | 27 | 15586.83 | 1.54 |
| 3 | CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr+EarlyTrendGate | YES | 44 | 11036.56 | 1.23 | 17 | 9226.64 | 1.43 | 12379.29 | 27 | 15586.83 | 1.54 |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr+EarlyTrendGate | YES | 44 | 11036.56 | 1.23 | 17 | 9226.64 | 1.43 | 12379.29 | 27 | 15586.83 | 1.54 |
| 5 | CALL|MIX|Off-Pcr+EarlyTrendGate | YES | 32 | 1756.71 | 1.05 | 16 | 7558.96 | 1.37 | 9366.59 | 23 | 18655.09 | 1.88 |
| 6 | CALL|MIX|Off-Rsi+Pcr+EarlyTrendGate | YES | 38 | 21313.7 | 1.56 | 16 | 7558.96 | 1.37 | 9366.59 | 24 | 23137.47 | 2.09 |
| 7 | CALL|MIX|Off-OpeningRsi+Pcr+EarlyTrendGate | YES | 30 | 661.35 | 1.02 | 15 | 7558.96 | 1.37 | 9366.59 | 20 | 22834.88 | 2.35 |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+Pcr+EarlyTrendGate | YES | 38 | 21313.7 | 1.56 | 16 | 7558.96 | 1.37 | 9366.59 | 24 | 23137.47 | 2.09 |
| 9 | CALL|MIX|Off-Atr+Pcr+EarlyTrendGate | YES | 32 | 5750.91 | 1.16 | 16 | 7558.96 | 1.37 | 9366.59 | 23 | 18655.09 | 1.88 |
| 10 | CALL|MIX|Off-Rsi+Atr+Pcr+EarlyTrendGate | YES | 38 | 25307.9 | 1.69 | 16 | 7558.96 | 1.37 | 9366.59 | 24 | 23137.47 | 2.09 |

## CALL SENSEX

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 32 of 64 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Off-EarlyTrendGate | no | 19 | 2836.64 | 1.24 | 26 | -1887.28 | 0.95 | 14982.95 | 24 | 6714.1 | 1.41 |
| 2 | CALL|MIX|Off-Rsi+EarlyTrendGate | no | 31 | 20109.15 | 2.28 | 26 | -1887.28 | 0.95 | 14982.95 | 28 | 10124.34 | 1.52 |
| 3 | CALL|MIX|Off-OpeningRsi+EarlyTrendGate | no | 15 | -2808.29 | 0.76 | 26 | -1887.28 | 0.95 | 14982.95 | 19 | 7080.12 | 1.53 |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi+EarlyTrendGate | no | 31 | 20109.15 | 2.28 | 26 | -1887.28 | 0.95 | 14982.95 | 28 | 10124.34 | 1.52 |
| 5 | CALL|MIX|Off-Atr+EarlyTrendGate | no | 20 | 1663.47 | 1.13 | 26 | -1887.28 | 0.95 | 14982.95 | 24 | 6714.1 | 1.41 |
| 6 | CALL|MIX|Off-Rsi+Atr+EarlyTrendGate | no | 32 | 18935.99 | 2.12 | 26 | -1887.28 | 0.95 | 14982.95 | 28 | 10124.34 | 1.52 |
| 7 | CALL|MIX|Off-OpeningRsi+Atr+EarlyTrendGate | no | 16 | -3981.45 | 0.7 | 26 | -1887.28 | 0.95 | 14982.95 | 19 | 7080.12 | 1.53 |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+Atr+EarlyTrendGate | no | 32 | 18935.99 | 2.12 | 26 | -1887.28 | 0.95 | 14982.95 | 28 | 10124.34 | 1.52 |
| 9 | CALL|MIX|Off-OpeningRsi+EmaGap+Pcr+EarlyTrendGate | no | 25 | 12098.61 | 1.82 | 31 | -6698.2 | 0.83 | 14189.37 | 37 | 5893.96 | 1.22 |
| 10 | CALL|MIX|Off-OpeningRsi+EmaGap+Atr+Pcr+EarlyTrendGate | no | 26 | 10925.45 | 1.69 | 31 | -6698.2 | 0.83 | 14189.37 | 37 | 9256.76 | 1.37 |

## CALL ALL

**Best validated: CALL|MIX|Off-Rsi+EmaGap+Pcr** - validation net 5041.77, PF 1.21; held-out test net 10541.07, PF 3.56 on 6 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|MIX|Off-Rsi+EmaGap+Pcr | YES | 15 | 22486.6 | 3.05 | 18 | 5041.77 | 1.21 | 7971.59 | 6 | 10541.07 | 3.56 |
| 2 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | YES | 15 | 22486.6 | 3.05 | 18 | 5041.77 | 1.21 | 7971.59 | 6 | 10541.07 | 3.56 |
| 3 | CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr | YES | 15 | 22486.6 | 3.05 | 18 | 5041.77 | 1.21 | 7971.59 | 6 | 10541.07 | 3.56 |
| 4 | CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr | YES | 15 | 22486.6 | 3.05 | 18 | 5041.77 | 1.21 | 7971.59 | 6 | 10541.07 | 3.56 |
| 5 | CALL|MIX|Off-Pcr+EarlyTrendGate | no | 54 | 15470.13 | 1.32 | 31 | -138.53 | 1 | 15511.67 | 48 | 27862.82 | 1.8 |
| 6 | CALL|MIX|Off-Rsi+Pcr+EarlyTrendGate | no | 65 | 41991.57 | 1.78 | 31 | -138.53 | 1 | 15511.67 | 52 | 31201.61 | 1.82 |
| 7 | CALL|MIX|Off-OpeningRsi+Pcr+EarlyTrendGate | no | 49 | 7633.17 | 1.16 | 30 | -138.53 | 1 | 15511.67 | 42 | 24588.63 | 1.76 |
| 8 | CALL|MIX|Off-Rsi+OpeningRsi+Pcr+EarlyTrendGate | no | 65 | 41991.57 | 1.78 | 31 | -138.53 | 1 | 15511.67 | 52 | 31201.61 | 1.82 |
| 9 | CALL|MIX|Off-Atr+Pcr+EarlyTrendGate | no | 54 | 19464.33 | 1.42 | 31 | -138.53 | 1 | 15511.67 | 48 | 31225.62 | 1.94 |
| 10 | CALL|MIX|Off-Rsi+Atr+Pcr+EarlyTrendGate | no | 65 | 45985.78 | 1.88 | 31 | -138.53 | 1 | 15511.67 | 52 | 34564.41 | 1.95 |

## PUT NIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 32 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 24 of 32 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|MIX|Off-Rsi+Pcr | no | 50 | -12152.7 | 0.79 | 40 | 22435.44 | 1.52 | 10626.61 | 27 | -8670.32 | 0.75 |
| 2 | PUT|MIX|Off-Rsi+OpeningRsi+Pcr | no | 50 | -12152.7 | 0.79 | 40 | 22435.44 | 1.52 | 10626.61 | 27 | -8670.32 | 0.75 |
| 3 | PUT|MIX|Off-Rsi+Atr+Pcr | no | 50 | -12152.7 | 0.79 | 40 | 22435.44 | 1.52 | 10626.61 | 27 | -8670.32 | 0.75 |
| 4 | PUT|MIX|Off-Rsi+OpeningRsi+Atr+Pcr | no | 50 | -12152.7 | 0.79 | 40 | 22435.44 | 1.52 | 10626.61 | 27 | -8670.32 | 0.75 |
| 5 | PUT|MIX|Off-Rsi | no | 48 | -8089.63 | 0.85 | 30 | 16187.95 | 1.44 | 10626.61 | 27 | -8670.32 | 0.75 |
| 6 | PUT|MIX|Off-Rsi+OpeningRsi | no | 48 | -8089.63 | 0.85 | 30 | 16187.95 | 1.44 | 10626.61 | 27 | -8670.32 | 0.75 |
| 7 | PUT|MIX|Off-Rsi+Atr | no | 48 | -8089.63 | 0.85 | 30 | 16187.95 | 1.44 | 10626.61 | 27 | -8670.32 | 0.75 |
| 8 | PUT|MIX|Off-Rsi+OpeningRsi+Atr | no | 48 | -8089.63 | 0.85 | 30 | 16187.95 | 1.44 | 10626.61 | 27 | -8670.32 | 0.75 |
| 9 | PUT|MIX|Off-Rsi+EmaGap+Pcr | no | 47 | -5908.92 | 0.89 | 44 | 11964.03 | 1.23 | 14389.28 | 30 | -8152.26 | 0.78 |
| 10 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | no | 47 | -5908.92 | 0.89 | 44 | 11964.03 | 1.23 | 14389.28 | 30 | -8152.26 | 0.78 |

## PUT SENSEX

**Best validated: PUT|MIX|Off-Rsi+EmaGap** - validation net 23181.63, PF 2.64; held-out test net -2813.44, PF 0.87 on 25 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|MIX|Off-Rsi+EmaGap | YES | 50 | 24665.82 | 1.8 | 24 | 23181.63 | 2.64 | 4854.42 | 25 | -2813.44 | 0.87 |
| 2 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap | YES | 50 | 24665.82 | 1.8 | 24 | 23181.63 | 2.64 | 4854.42 | 25 | -2813.44 | 0.87 |
| 3 | PUT|MIX|Off-Rsi+EmaGap+Atr | YES | 51 | 28151.68 | 1.93 | 24 | 23181.63 | 2.64 | 4854.42 | 25 | -7397.26 | 0.69 |
| 4 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr | YES | 51 | 28151.68 | 1.93 | 24 | 23181.63 | 2.64 | 4854.42 | 25 | -7397.26 | 0.69 |
| 5 | PUT|MIX|Off-Rsi | YES | 50 | 25804.05 | 1.91 | 24 | 23075.24 | 2.64 | 4854.42 | 25 | -2813.44 | 0.87 |
| 6 | PUT|MIX|Off-Rsi+OpeningRsi | YES | 50 | 25804.05 | 1.91 | 24 | 23075.24 | 2.64 | 4854.42 | 25 | -2813.44 | 0.87 |
| 7 | PUT|MIX|Off-Rsi+Atr | YES | 51 | 29289.91 | 2.05 | 24 | 23075.24 | 2.64 | 4854.42 | 25 | -7397.26 | 0.69 |
| 8 | PUT|MIX|Off-Rsi+OpeningRsi+Atr | YES | 51 | 29289.91 | 2.05 | 24 | 23075.24 | 2.64 | 4854.42 | 25 | -7397.26 | 0.69 |
| 9 | PUT|MIX|Off-Rsi+EmaGap+Pcr | YES | 53 | 26601.44 | 1.82 | 49 | 16215.49 | 1.49 | 7617.25 | 26 | -4408.38 | 0.82 |
| 10 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | YES | 53 | 26601.44 | 1.82 | 49 | 16215.49 | 1.49 | 7617.25 | 26 | -4408.38 | 0.82 |

## PUT ALL

**Best validated: PUT|MIX|Off-Rsi+EmaGap+Pcr** - validation net 41952.07, PF 2.02; held-out test net -12497.98, PF 0.76 on 42 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|MIX|Off-Rsi+EmaGap+Pcr | YES | 73 | 22088.07 | 1.36 | 51 | 41952.07 | 2.02 | 6487.19 | 42 | -12497.98 | 0.76 |
| 2 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | YES | 73 | 22088.07 | 1.36 | 51 | 41952.07 | 2.02 | 6487.19 | 42 | -12497.98 | 0.76 |
| 3 | PUT|MIX|Off-Rsi+EmaGap+Atr+Pcr | YES | 74 | 21810.81 | 1.35 | 51 | 41952.07 | 2.02 | 6487.19 | 42 | -12497.98 | 0.76 |
| 4 | PUT|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr | YES | 74 | 21810.81 | 1.35 | 51 | 41952.07 | 2.02 | 6487.19 | 42 | -12497.98 | 0.76 |
| 5 | PUT|MIX|Off-Rsi+Pcr | YES | 75 | 14830.11 | 1.23 | 51 | 39170.02 | 2.01 | 9562.73 | 37 | -2804.52 | 0.93 |
| 6 | PUT|MIX|Off-Rsi+OpeningRsi+Pcr | YES | 75 | 14830.11 | 1.23 | 51 | 39170.02 | 2.01 | 9562.73 | 37 | -2804.52 | 0.93 |
| 7 | PUT|MIX|Off-Rsi+Atr+Pcr | YES | 76 | 14552.85 | 1.22 | 51 | 39170.02 | 2.01 | 9562.73 | 37 | -2804.52 | 0.93 |
| 8 | PUT|MIX|Off-Rsi+OpeningRsi+Atr+Pcr | YES | 76 | 14552.85 | 1.22 | 51 | 39170.02 | 2.01 | 9562.73 | 37 | -2804.52 | 0.93 |
| 9 | PUT|MIX|Off-Rsi | YES | 73 | 15866.94 | 1.26 | 40 | 25236.26 | 1.63 | 10285.26 | 37 | -3131.77 | 0.93 |
| 10 | PUT|MIX|Off-Rsi+OpeningRsi | YES | 73 | 15866.94 | 1.26 | 40 | 25236.26 | 1.63 | 10285.26 | 37 | -3131.77 | 0.93 |

