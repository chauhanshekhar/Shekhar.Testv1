# Backtest ranking (training and validation only; the test column was never used to rank)

Stop model Live; premium OptionHistoryWhereAvailable (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); selector Replay; costs modelled; settings = current StrategySettings applied to every day; bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).

## CALL NIFTY

Baseline: trades 1, net 0, PF NULL, validation net 0 (PF NULL, 0 trades), test net 0 (PF NULL, 1 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 26 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## CALL BANKNIFTY

Baseline: trades 21, net 15000.61, PF 1.54, validation net -10858.69 (PF 0.25, 7 trades), test net 25859.3 (PF 2.92, 14 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 13 of 26 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoEarlyTrendGate | no | 128 | -4038.2 | 0.97 | 50 | 3964.25 | 1.07 | 19059.53 | 89 | 50450.96 | 1.58 |
| 2 | CALL|NoPcr+GapTrend:EarlyTrendOnly | no | 73 | -4706.98 | 0.94 | 25 | -7310.25 | 0.75 | 15723 | 40 | 41418.55 | 2.2 |
| 3 | CALL|NoPcr+GapTrend:All | no | 89 | -22743.08 | 0.77 | 30 | -10300 | 0.7 | 16145.27 | 52 | 35145.13 | 1.73 |
| 4 | CALL|NoPcr+NoEmaGap | no | 67 | 30476.7 | 1.58 | 25 | -11485.24 | 0.6 | 13989.8 | 26 | 26382.5 | 1.84 |
| 5 | CALL|NoPcr+Baseline | no | 50 | 3857.58 | 1.08 | 22 | -12768.37 | 0.53 | 15723 | 20 | 32753.26 | 2.94 |
| 6 | CALL|NoPcr+Sideways5mAgrees | no | 50 | 3857.58 | 1.08 | 22 | -12768.37 | 0.53 | 15723 | 20 | 32753.26 | 2.94 |
| 7 | CALL|NoPcr+SidewaysOff | no | 50 | 3857.58 | 1.08 | 22 | -12768.37 | 0.53 | 15723 | 20 | 32753.26 | 2.94 |
| 8 | CALL|NoPcr+RsiCeiling80 | no | 52 | -531.33 | 0.99 | 22 | -12768.37 | 0.53 | 15723 | 20 | 32753.26 | 2.94 |
| 9 | CALL|NoPcr+GapTrend:RsiOnly | no | 50 | 3857.58 | 1.08 | 22 | -12768.37 | 0.53 | 15723 | 20 | 32753.26 | 2.94 |
| 10 | CALL|NoPcr+GapTrend:SidewaysOnly | no | 50 | 3857.58 | 1.08 | 22 | -12768.37 | 0.53 | 15723 | 20 | 32753.26 | 2.94 |

## CALL SENSEX

Baseline: trades 13, net 26820.36, PF 7.56, validation net 5553.56 (PF NULL, 3 trades), test net 22430.93 (PF 8.66, 9 trades).
**Best validated: CALL|NoPcr+NoEarlyTrendGate** - validation net 23704.21, PF 2.35; held-out test net 45810.99, PF 1.5 on 120 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoEarlyTrendGate | YES | 96 | 4235.08 | 1.06 | 35 | 23704.21 | 2.35 | 5850.93 | 120 | 45810.99 | 1.5 |
| 2 | CALL|NoPcr+GapTrend:All | YES | 60 | 11962.25 | 1.33 | 26 | 17366.51 | 2.36 | 2615.35 | 29 | 10024.14 | 1.35 |
| 3 | CALL|NoPcr+GapTrend:EarlyTrendOnly | YES | 53 | 9692.67 | 1.3 | 25 | 17223.86 | 2.33 | 3789.87 | 28 | 11860.76 | 1.44 |
| 4 | CALL|NoPcr+Baseline | no | 29 | -620.44 | 0.97 | 19 | 15935.75 | 2.8 | 2605.81 | 17 | 14551.94 | 1.99 |
| 5 | CALL|NoPcr+Sideways5mAgrees | no | 29 | -620.44 | 0.97 | 19 | 15935.75 | 2.8 | 2605.81 | 17 | 14551.94 | 1.99 |
| 6 | CALL|NoPcr+Gap5pts | no | 29 | -620.44 | 0.97 | 19 | 15935.75 | 2.8 | 2605.81 | 17 | 14551.94 | 1.99 |
| 7 | CALL|NoPcr+NoEmaGap | no | 29 | -620.44 | 0.97 | 19 | 15935.75 | 2.8 | 2605.81 | 17 | 14551.94 | 1.99 |
| 8 | CALL|NoPcr+RsiCeiling80 | no | 29 | -620.44 | 0.97 | 19 | 15935.75 | 2.8 | 2605.81 | 17 | 14551.94 | 1.99 |
| 9 | CALL|NoPcr+GapTrend:RsiOnly | no | 29 | -620.44 | 0.97 | 19 | 15935.75 | 2.8 | 2605.81 | 17 | 14551.94 | 1.99 |
| 10 | CALL|NoPcr+GapTrend:SidewaysOnly | no | 29 | -620.44 | 0.97 | 19 | 15935.75 | 2.8 | 2605.81 | 17 | 14551.94 | 1.99 |

## CALL ALL

Baseline: trades 31, net 31775.65, PF 1.99, validation net -5305.14 (PF 0.63, 10 trades), test net 38244.92 (PF 3.34, 20 trades).
**Best validated: CALL|NoPcr+NoEmaGap** - validation net 7326.71, PF 1.21; held-out test net 45364.49, PF 2.44 on 33 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CALL|NoPcr+NoEmaGap | YES | 82 | 31287.91 | 1.54 | 41 | 7326.71 | 1.21 | 11928.26 | 33 | 45364.49 | 2.44 |
| 2 | CALL|NoPcr+GapTrend:EarlyTrendOnly | YES | 107 | 7610.84 | 1.08 | 45 | 7095.48 | 1.2 | 13991.14 | 53 | 58556.3 | 2.4 |
| 3 | CALL|NoPcr+Baseline | YES | 67 | 4000.5 | 1.07 | 38 | 5058.27 | 1.16 | 13991.14 | 30 | 43697.02 | 2.7 |
| 4 | CALL|NoPcr+Sideways5mAgrees | YES | 67 | 4000.5 | 1.07 | 38 | 5058.27 | 1.16 | 13991.14 | 30 | 43697.02 | 2.7 |
| 5 | CALL|NoPcr+GapTrend:RsiOnly | YES | 67 | 4000.5 | 1.07 | 38 | 5058.27 | 1.16 | 13991.14 | 30 | 43697.02 | 2.7 |
| 6 | CALL|NoPcr+GapTrend:SidewaysOnly | YES | 67 | 4000.5 | 1.07 | 38 | 5058.27 | 1.16 | 13991.14 | 30 | 43697.02 | 2.7 |
| 7 | CALL|NoPcr+GapTrend:EmaGapOnly | YES | 67 | 4000.5 | 1.07 | 38 | 5058.27 | 1.16 | 13991.14 | 30 | 43697.02 | 2.7 |
| 8 | CALL|NoPcr+Gap5pts | YES | 71 | 17271.22 | 1.32 | 39 | 3092.07 | 1.08 | 14575.12 | 29 | 53365.87 | 3.37 |
| 9 | CALL|NoPcr+Combined:Sideways5m+Gap5+Rsi | YES | 74 | 11325.7 | 1.19 | 39 | 3092.07 | 1.08 | 14575.12 | 29 | 53365.87 | 3.37 |
| 10 | CALL|NoPcr+NoEarlyTrendGate | no | 177 | -12131.06 | 0.93 | 70 | 16813.02 | 1.28 | 14346.38 | 146 | 98346.72 | 1.86 |

## PUT NIFTY

Baseline: trades 26, net 2638.81, PF 1.08, validation net -361.06 (PF 0.95, 6 trades), test net 628.72 (PF 1.03, 16 trades).
**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least 15 trades in each). 0 of 22 configurations had enough trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## PUT BANKNIFTY

Baseline: trades 202, net 40609.7, PF 1.21, validation net 18323.8 (PF 1.94, 28 trades), test net -15178.94 (PF 0.8, 60 trades).
**Best validated: PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi** - validation net 30293.12, PF 2.31; held-out test net -7401.16, PF 0.92 on 75 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi | YES | 136 | 24300.97 | 1.19 | 33 | 30293.12 | 2.31 | 6531.11 | 75 | -7401.16 | 0.92 |
| 2 | PUT|NoPcr+RsiFloor20 | YES | 131 | 30095.49 | 1.24 | 32 | 29032.88 | 2.34 | 6552.04 | 76 | -12704.92 | 0.86 |
| 3 | PUT|NoPcr+Baseline | YES | 114 | 37464.85 | 1.36 | 29 | 26138.48 | 2.34 | 6552.04 | 69 | -18281.14 | 0.78 |
| 4 | PUT|NoPcr+Sideways5mAgrees | YES | 114 | 37464.85 | 1.36 | 29 | 26138.48 | 2.34 | 6552.04 | 69 | -18281.14 | 0.78 |
| 5 | PUT|NoPcr+SidewaysOff | YES | 114 | 37464.85 | 1.36 | 29 | 26138.48 | 2.34 | 6552.04 | 69 | -18281.14 | 0.78 |
| 6 | PUT|NoPcr+GapTrend:SidewaysOnly | YES | 114 | 37464.85 | 1.36 | 29 | 26138.48 | 2.34 | 6552.04 | 69 | -18281.14 | 0.78 |
| 7 | PUT|NoPcr+GapTrend:EmaGapOnly | YES | 115 | 40772.19 | 1.4 | 29 | 26138.48 | 2.34 | 6552.04 | 69 | -18281.14 | 0.78 |
| 8 | PUT|NoPcr+Gap5pts | YES | 119 | 31670.33 | 1.29 | 31 | 26026.77 | 2.16 | 7903.05 | 68 | -12977.38 | 0.84 |
| 9 | PUT|Combined:Sideways5m+Gap5+Rsi | YES | 136 | 24300.97 | 1.19 | 32 | 22478.44 | 1.97 | 6531.11 | 65 | -5655.64 | 0.93 |
| 10 | PUT|NoPcr+NoEmaGap | YES | 121 | 39113.94 | 1.36 | 35 | 22283.01 | 1.83 | 10548.75 | 70 | -9415.08 | 0.89 |

## PUT SENSEX

Baseline: trades 99, net 48196.63, PF 1.78, validation net 6480.81 (PF 1.41, 19 trades), test net 32940.94 (PF 1.75, 71 trades).
**Best validated: PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi** - validation net 23269.41, PF 1.55; held-out test net 38220.11, PF 1.71 on 87 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi | YES | 84 | 21396.79 | 1.46 | 68 | 23269.41 | 1.55 | 9735.42 | 87 | 38220.11 | 1.71 |
| 2 | PUT|NoPcr+RsiFloor20 | YES | 82 | 24464.99 | 1.56 | 65 | 16761.28 | 1.4 | 8361.62 | 86 | 32850.62 | 1.58 |
| 3 | PUT|NoPcr+GapTrend:All | YES | 70 | 26923.83 | 1.75 | 60 | 15591.38 | 1.41 | 9933.96 | 83 | 36260.21 | 1.64 |
| 4 | PUT|NoPcr+GapTrend:RsiOnly | YES | 70 | 26923.83 | 1.75 | 58 | 15537.3 | 1.41 | 9933.96 | 83 | 36260.21 | 1.64 |
| 5 | PUT|NoPcr+SidewaysOff | YES | 66 | 24122.48 | 1.78 | 48 | 3136.4 | 1.1 | 13541.14 | 85 | 40853.13 | 1.74 |
| 6 | PUT|NoPcr+GapTrend:SidewaysOnly | YES | 64 | 22041.77 | 1.7 | 46 | 2519.57 | 1.08 | 12122.64 | 83 | 36260.21 | 1.64 |
| 7 | PUT|NoPcr+Sideways5mAgrees | YES | 65 | 20898.12 | 1.64 | 48 | 2491.56 | 1.08 | 12150.65 | 84 | 41629.7 | 1.77 |
| 8 | PUT|NoPcr+Baseline | YES | 64 | 22041.77 | 1.7 | 46 | 2465.48 | 1.08 | 12176.73 | 83 | 36260.21 | 1.64 |
| 9 | PUT|NoPcr+Gap5pts | YES | 64 | 22041.77 | 1.7 | 46 | 2465.48 | 1.08 | 12176.73 | 83 | 36260.21 | 1.64 |
| 10 | PUT|NoPcr+NoEmaGap | YES | 64 | 22041.77 | 1.7 | 46 | 2465.48 | 1.08 | 12176.73 | 83 | 36260.21 | 1.64 |

## PUT ALL

Baseline: trades 273, net 76070.02, PF 1.31, validation net 21276.6 (PF 1.62, 46 trades), test net 20914.22 (PF 1.2, 107 trades).
**Best validated: PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi** - validation net 55643.78, PF 2.06; held-out test net 46491.41, PF 1.41 on 123 trades.

| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | PUT|NoPcr+Combined:Sideways5m+Gap5+Rsi | YES | 177 | 22181.23 | 1.15 | 89 | 55643.78 | 2.06 | 9535.84 | 123 | 46491.41 | 1.41 |
| 2 | PUT|Combined:Sideways5m+Gap5+Rsi | YES | 143 | 21725.08 | 1.16 | 65 | 45221.02 | 2.04 | 7664.87 | 112 | 34313.81 | 1.32 |
| 3 | PUT|NoPcr+RsiFloor20 | YES | 172 | 26597.33 | 1.18 | 85 | 44655.67 | 1.87 | 7664.87 | 123 | 36755.77 | 1.31 |
| 4 | PUT|RsiFloor20 | YES | 138 | 27519.6 | 1.21 | 64 | 40165.38 | 1.93 | 7664.87 | 112 | 25255.85 | 1.22 |
| 5 | PUT|NoPcr+GapTrend:RsiOnly | YES | 163 | 26005.99 | 1.18 | 75 | 32032.33 | 1.67 | 9476.51 | 119 | 37948.19 | 1.34 |
| 6 | PUT|NoPcr+GapTrend:All | YES | 164 | 33646.14 | 1.24 | 78 | 29964.6 | 1.6 | 11598.33 | 119 | 37948.19 | 1.34 |
| 7 | PUT|GapTrend:All | YES | 132 | 42740.63 | 1.37 | 60 | 29353.59 | 1.67 | 9432.1 | 107 | 20914.22 | 1.2 |
| 8 | PUT|GapTrend:RsiOnly | YES | 130 | 36646.55 | 1.31 | 59 | 29299.5 | 1.67 | 9432.1 | 107 | 20914.22 | 1.2 |
| 9 | PUT|Sideways5mAgrees | YES | 120 | 33879.2 | 1.32 | 46 | 23952.21 | 1.72 | 6552.04 | 108 | 24668.42 | 1.24 |
| 10 | PUT|SidewaysOff | YES | 121 | 34146.16 | 1.32 | 46 | 23952.21 | 1.72 | 6552.04 | 107 | 27014.8 | 1.27 |

