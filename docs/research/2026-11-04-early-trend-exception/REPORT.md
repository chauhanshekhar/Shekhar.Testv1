# EarlyTrendGate exception by 5m EMA gap / ATR5m (Version C), 2026-11-04

`--early-trend-exception --side CALL --cost-per-order 20 --slippage-points 1 --dump-trades`. A = live gate, B = gate off, C1-C5 = allow a CALL while (5m EMA20 - EMA50) / ATR5m(14, Wilder) < 0.10/0.20/0.30/0.40/0.50 (experimental values, not optimised). Everything else live (RSI bands, exits, one-position rule). Net after costs. 21 configs. Full per-symbol tables in grid.csv / ranking.md.

## Result (all symbols, shared one-position rule)
| Opening | Arm | Trades | Net | PF | DD | SL-first | Train / Valid / Test net |
|---|---|---|---|---|---|---|---|
| GapUp | A live | 40 | +19,323 | 2.14 | 7,060 | 38% | 2,887 / 6,998 / 21,738 |
| GapUp | B off | 130 | +20,240 | 1.68 | 9,850 | 41% | 6,490 / 8,710 / 47,200 |
| GapUp | C 0.20 | 48 | +18,839 | 2.02 | 8,813 | 40% | 74 / 7,226 / 26,298 |
| GapUp | C 0.30 | 49 | +21,740 | 2.12 | 8,813 | 39% | 3,395 / 7,226 / 26,298 |
| GapUp | C 0.50 | 56 | +17,909 | 1.85 | 8,813 | 39% | 8,353 / 7,226 / 26,298 |
| Normal | A live | 140 | -23,809 | 1.15 | 33,807 | 52% | 3,393 / 9,612 / 10,326 |
| Normal | C 0.20 | 157 | -11,879 | 1.25 | 19,830 | 49% | -1,029 / 21,654 / 20,495 |
| Normal | C 0.30 | 171 | -22,357 | 1.19 | 21,486 | 49% | -7,425 / 27,146 / 15,102 |
| Normal | C 0.40 | 177 | -32,220 | 1.14 | 23,130 | 50% | -8,984 / 25,141 / 10,323 |
| GapDown | A live | 65 | -635 | 1.43 | 17,088 | 40% | 3,954 / 243 / 17,467 |
| GapDown | C 0.30 | 68 | +12,577 | 1.70 | 14,561 | 41% | 13,631 / -1,439 / 25,084 |

## Against the stated criteria
- **Gap-up:** no threshold improves validation materially (C 0.20/0.30 +3%, C 0.10 worse), profit factor is flat to lower, drawdown rises (7,060 -> 8,813). Trades added: C 0.20 n=12, net -496; C 0.30 n=13, +2,406; C 0.50 n=22, -3,414. Not an improvement. BANKNIFTY gap-up has only 9-15 trades in any arm.
- **Normal:** the added trades lose (C 0.20 n=25 -6,850 PF 0.76; C 0.30 n=43 -14,703 PF 0.71). Normal CALL stays net negative in every arm; result is non-monotonic in the threshold (0.10 and 0.40 worse than live), i.e. noise. Training is worse than live in every C arm.
- **Gap-down:** added trades are strong (n=4-11, PF 3-6) but validation is worse than live at 0.30-0.50 (-1,439 vs +243) and the sample is tiny. Not gap-up scope.
- **Verdict: no threshold passes. Keep the live EarlyTrendGate.**

## "How many profitable morning CALLs did the gate block?" (per-symbol trade lists, before the one-position rule; morning = entry before 10:30 IST)
| Opening | Blocked trades (B not in A) | Blocked morning only |
|---|---|---|
| GapUp | n=123, net -9,512, PF 0.92 | n=62, 29 winners, net +21,106, PF 1.44, 18 days, best day 8,611 |
| Normal | n=267, net -62,574, PF 0.79 | n=84, net -22,238, PF 0.80 |
| GapDown | n=42, net -2,813, PF 0.94 | n=2 |

So the missed-move problem is real only on gap-up mornings: the gate does block about 29 winning morning gap-up CALLs. But across the whole gap-up day the blocked trades lose, and on normal days the gate is right in the morning too. A "morning gap-up only" relaxation is a new hypothesis chosen AFTER seeing this table: it needs an unfiltered base-rate check and a chronological split before it counts as evidence. These numbers are before the one-position rule and before checking train/validation separately.

## Limits
21 configs; ATR5m is a research construct (live system has no 5m ATR); per-symbol trade lists skip the shared one-position rule, so added/blocked counts are approximate. Nothing in live settings or code changed.
