# Condition search on live-exit replay (2026-10-07..2026-10-28, 15 sessions)

Net is after costs (CostRs from the replay). Split train <2026-10-21 | validation <2026-10-26 | holdout rest. Simulated outcomes only; no live signals or trades used.

Pass = N >= 20, all three periods > 0, PF > 1.2, profitable without its best day, beats live in total and holdout (same side and scope).

## Live baseline

| Configuration | Scope | N | Days | WinPct | Gross | Net | PF | Exp | MaxDD | NetExBestDay | TrainNet | ValNet | HoldNet | LiveNet | LiveHold | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| CALL|MIX|Live | ALL | 45 | 12 | 33.3 | 11625 | -4315 | 0.94 | -95.9 | 33919 | -17999 | 12369 | -5584 | -11099 | -4315 | -11099 | False |
| CALL|MIX|Live | BANKNIFTY | 30 | 10 | 40.0 | 22393 | 12033 | 1.27 | 401.1 | 26646 | -1651 | 12821 | 13476 | -14263 | 12033 | -14263 | False |
| CALL|MIX|Live | NIFTY | 15 | 5 | 46.7 | 15142 | 4222 | 1.17 | 281.5 | 10866 | -772 | 3045 | -6632 | 7809 | 4222 | 7809 | False |
| CALL|MIX|Live | SENSEX | 25 | 8 | 40.0 | 23045 | 16545 | 1.62 | 661.8 | 9006 | 7768 | -1324 | 7950 | 9918 | 16545 | 9918 | False |
| PUT|MIX|Live | ALL | 107 | 14 | 43.0 | 31875 | -5285 | 0.96 | -49.4 | 26918 | -22527 | -2568 | 9618 | -12335 | -5285 | -12335 | False |
| PUT|MIX|Live | BANKNIFTY | 60 | 14 | 31.7 | -8724 | -28884 | 0.67 | -481.4 | 32942 | -39839 | -28886 | 10799 | -10797 | -28884 | -10797 | False |
| PUT|MIX|Live | NIFTY | 21 | 8 | 42.9 | 12723 | -2117 | 0.93 | -100.8 | 11673 | -5524 | -1171 | 2630 | -3576 | -2117 | -3576 | False |
| PUT|MIX|Live | SENSEX | 74 | 12 | 51.4 | 46302 | 26902 | 1.52 | 363.5 | 8356 | 15861 | 23378 | 6186 | -2661 | 26902 | -2661 | False |

## Passing candidates (12)

| Configuration | Scope | N | Days | WinPct | Gross | Net | PF | Exp | MaxDD | NetExBestDay | TrainNet | ValNet | HoldNet | LiveNet | LiveHold | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+EmaGap+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-EmaGap+Atr+Pcr | NIFTY | 20 | 7 | 55.0 | 42871 | 27751 | 1.99 | 1387.6 | 7783 | 9996 | 4290 | 6553 | 16908 | 4222 | 7809 | True |
| CALL|MIX|Off-EmaGap+Pcr | NIFTY | 20 | 7 | 55.0 | 42871 | 27751 | 1.99 | 1387.6 | 7783 | 9996 | 4290 | 6553 | 16908 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr | SENSEX | 57 | 13 | 43.9 | 39365 | 24565 | 1.47 | 431.0 | 13494 | 16661 | 10070 | 561 | 13935 | 16545 | 9918 | True |
| CALL|MIX|Off-Rsi+EmaGap+Pcr | SENSEX | 54 | 12 | 44.4 | 40228 | 26128 | 1.53 | 483.9 | 13494 | 18225 | 11633 | 561 | 13935 | 16545 | 9918 | True |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr | SENSEX | 57 | 13 | 43.9 | 39365 | 24565 | 1.47 | 431.0 | 13494 | 16661 | 10070 | 561 | 13935 | 16545 | 9918 | True |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | SENSEX | 54 | 12 | 44.4 | 40228 | 26128 | 1.53 | 483.9 | 13494 | 18225 | 11633 | 561 | 13935 | 16545 | 9918 | True |
| PUT|MIX|Off-Rsi | SENSEX | 142 | 15 | 45.1 | 66253 | 29553 | 1.27 | 208.1 | 24528 | 13212 | 13306 | 15483 | 765 | 26902 | -2661 | True |
| PUT|MIX|Off-Rsi+OpeningRsi | SENSEX | 142 | 15 | 45.1 | 66253 | 29553 | 1.27 | 208.1 | 24528 | 13212 | 13306 | 15483 | 765 | 26902 | -2661 | True |

## CALL: top 10 by net (context, pass or not)

| Configuration | Scope | N | Days | WinPct | Gross | Net | PF | Exp | MaxDD | NetExBestDay | TrainNet | ValNet | HoldNet | LiveNet | LiveHold | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+EmaGap+Pcr | NIFTY | 22 | 8 | 63.6 | 60237 | 42877 | 2.74 | 1949.0 | 7783 | 25122 | 12436 | 6247 | 24194 | 4222 | 7809 | True |
| CALL|MIX|Off-Rsi+EmaGap+Atr | NIFTY | 18 | 7 | 66.7 | 55582 | 41582 | 3.26 | 2310.1 | 4850 | 23827 | 13799 | 8237 | 19546 | 4222 | 7809 | False |
| CALL|MIX|Off-Rsi+EmaGap | NIFTY | 18 | 7 | 66.7 | 55582 | 41582 | 3.26 | 2310.1 | 4850 | 23827 | 13799 | 8237 | 19546 | 4222 | 7809 | False |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr | NIFTY | 18 | 7 | 66.7 | 55582 | 41582 | 3.26 | 2310.1 | 4850 | 23827 | 13799 | 8237 | 19546 | 4222 | 7809 | False |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap | NIFTY | 18 | 7 | 66.7 | 55582 | 41582 | 3.26 | 2310.1 | 4850 | 23827 | 13799 | 8237 | 19546 | 4222 | 7809 | False |
| CALL|MIX|Off-Rsi+OpeningRsi+EmaGap+Atr+Pcr+EarlyTrendGate | SENSEX | 175 | 15 | 45.1 | 83713 | 38813 | 1.27 | 221.8 | 25159 | 22846 | 28251 | -9191 | 19753 | 16545 | 9918 | False |
| CALL|MIX|Off-Rsi+EmaGap+Atr+Pcr+EarlyTrendGate | SENSEX | 175 | 15 | 45.1 | 83713 | 38813 | 1.27 | 221.8 | 25159 | 22846 | 28251 | -9191 | 19753 | 16545 | 9918 | False |

## PUT: top 10 by net (context, pass or not)

| Configuration | Scope | N | Days | WinPct | Gross | Net | PF | Exp | MaxDD | NetExBestDay | TrainNet | ValNet | HoldNet | LiveNet | LiveHold | Pass |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PUT|MIX|Off-Atr+Pcr | SENSEX | 89 | 13 | 50.6 | 57160 | 33960 | 1.55 | 381.6 | 8617 | 19592 | 28937 | 7684 | -2661 | 26902 | -2661 | False |
| PUT|MIX|Off-Pcr | SENSEX | 84 | 13 | 50.0 | 54975 | 33175 | 1.56 | 394.9 | 8617 | 19348 | 28152 | 7684 | -2661 | 26902 | -2661 | False |
| PUT|MIX|Off-EmaGap+Atr+Pcr | SENSEX | 95 | 14 | 49.5 | 56825 | 32225 | 1.5 | 339.2 | 8617 | 17857 | 26862 | 8024 | -2661 | 26902 | -2661 | False |
| PUT|MIX|Off-EmaGap+Pcr | SENSEX | 89 | 13 | 48.3 | 53467 | 30567 | 1.49 | 343.4 | 8617 | 16740 | 25204 | 8024 | -2661 | 26902 | -2661 | False |
| PUT|MIX|Off-Rsi+OpeningRsi | SENSEX | 142 | 15 | 45.1 | 66253 | 29553 | 1.27 | 208.1 | 24528 | 13212 | 13306 | 15483 | 765 | 26902 | -2661 | True |
| PUT|MIX|Off-Rsi | SENSEX | 142 | 15 | 45.1 | 66253 | 29553 | 1.27 | 208.1 | 24528 | 13212 | 13306 | 15483 | 765 | 26902 | -2661 | True |
| PUT|MIX|Off-EmaGap+Atr+Pcr | ALL | 129 | 14 | 51.9 | 73875 | 27915 | 1.23 | 216.4 | 20605 | 11444 | 27341 | 13825 | -13251 | -5285 | -12335 | False |
| PUT|MIX|Off-Atr | SENSEX | 79 | 12 | 51.9 | 48487 | 27687 | 1.51 | 350.5 | 8356 | 16389 | 24163 | 6186 | -2661 | 26902 | -2661 | False |
| PUT|MIX|Off-OpeningRsi+Atr+Pcr | SENSEX | 93 | 11 | 47.3 | 49453 | 25753 | 1.42 | 276.9 | 8641 | 14977 | 12156 | 16259 | -2661 | 26902 | -2661 | False |
| PUT|MIX|Off-OpeningRsi+EmaGap+Atr+Pcr | SENSEX | 93 | 11 | 47.3 | 49453 | 25753 | 1.42 | 276.9 | 8641 | 14977 | 12156 | 16259 | -2661 | 26902 | -2661 | False |

## Condition effect (net when OFF minus net when ON, averaged over the other combinations)

| Side | Scope | Condition | Pairs | AvgGainIfOff | OffBetterPct |
|---|---|---|---|---|---|
| CALL | ALL | Rsi | 32 | 14725 | 100 |
| CALL | ALL | Atr | 32 | 145 | 28 |
| CALL | ALL | OpeningRsi | 32 | -3054 | 0 |
| CALL | ALL | EmaGap | 32 | -10990 | 25 |
| CALL | ALL | EarlyTrendGate | 32 | -12853 | 25 |
| CALL | ALL | Pcr | 32 | -25713 | 0 |
| CALL | BANKNIFTY | Rsi | 32 | 1819 | 69 |
| CALL | BANKNIFTY | Atr | 32 | 763 | 50 |
| CALL | BANKNIFTY | OpeningRsi | 32 | -2938 | 0 |
| CALL | BANKNIFTY | EarlyTrendGate | 32 | -11326 | 25 |
| CALL | BANKNIFTY | Pcr | 32 | -19582 | 25 |
| CALL | BANKNIFTY | EmaGap | 32 | -19872 | 12 |
| CALL | NIFTY | EmaGap | 32 | 14870 | 88 |
| CALL | NIFTY | Rsi | 32 | 14668 | 100 |
| CALL | NIFTY | Atr | 32 | 0 | 0 |
| CALL | NIFTY | OpeningRsi | 32 | -2949 | 0 |
| CALL | NIFTY | Pcr | 32 | -7956 | 19 |
| CALL | NIFTY | EarlyTrendGate | 32 | -22553 | 0 |
| CALL | SENSEX | Rsi | 32 | 27424 | 100 |
| CALL | SENSEX | Pcr | 32 | 5071 | 47 |
| CALL | SENSEX | Atr | 32 | 1626 | 41 |
| CALL | SENSEX | OpeningRsi | 32 | -1506 | 0 |
| CALL | SENSEX | EmaGap | 32 | -4719 | 38 |
| CALL | SENSEX | EarlyTrendGate | 32 | -10120 | 19 |
| PUT | ALL | Pcr | 16 | 5151 | 50 |
| PUT | ALL | Atr | 16 | 1093 | 75 |
| PUT | ALL | OpeningRsi | 16 | -666 | 25 |
| PUT | ALL | EmaGap | 16 | -10143 | 25 |
| PUT | ALL | Rsi | 16 | -21300 | 19 |
| PUT | BANKNIFTY | Atr | 16 | 0 | 0 |
| PUT | BANKNIFTY | OpeningRsi | 16 | -1430 | 25 |
| PUT | BANKNIFTY | EmaGap | 16 | -7172 | 25 |
| PUT | BANKNIFTY | Rsi | 16 | -9195 | 38 |
| PUT | BANKNIFTY | Pcr | 16 | -13848 | 0 |
| PUT | NIFTY | EmaGap | 16 | 13746 | 88 |
| PUT | NIFTY | Pcr | 16 | 1842 | 75 |
| PUT | NIFTY | OpeningRsi | 16 | 792 | 50 |
| PUT | NIFTY | Atr | 16 | 0 | 0 |
| PUT | NIFTY | Rsi | 16 | -17360 | 0 |
| PUT | SENSEX | Pcr | 16 | -234 | 50 |
| PUT | SENSEX | Atr | 16 | -505 | 75 |
| PUT | SENSEX | OpeningRsi | 16 | -5149 | 0 |
| PUT | SENSEX | Rsi | 16 | -15551 | 19 |
| PUT | SENSEX | EmaGap | 16 | -16694 | 0 |
