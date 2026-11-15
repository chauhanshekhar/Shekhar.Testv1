# Option quote noise replay (2026-11-11)
Script: replay.py (read-only). 196 real signals 2026-10-07..11-11, minute premiums from OptionMinuteHistory.
Simplified bracket (stop, T1 full exit, 15:15), cost 2 pts/round trip, original stop distance estimated from the index stop (median ratio 0.996 vs true on 126 unmodified rows).
This is NOT live P&L (no partial/trail/step-lock); it isolates stop mechanics. Minute sampling understates tick-level spikes.

Variants: V1 limit at median of last 3 minute LTPs (skip if unfilled in 2 min); V2 stop needs 2 consecutive minute reads <= stop;
V3 stop needs index adverse move x |delta| >= 0.5 x stop distance.

| Group | V0 net pts / PF | V1 | V2 | V3 |
|---|---|---|---|---|
| SENSEX (n=89) | -149 / 0.89 | +82 / 1.12 (41% trades skipped) | +91 / 1.07 | -63 / 0.95 |
| SENSEX PUT (n=66) | +36 / 1.04 | +137 / 1.32 | +210 / 1.25 | +90 / 1.10 |
| NIFTY+BANKNIFTY (control, n=107) | -492 / 0.67 | -379 / 0.60 | -372 / 0.76 | -468 / 0.69 |

Pass criteria (set before running): beat V0 net pts in BOTH halves, PF >= V0, expectancy >= V0.
- V2 on SENSEX: passes (net and PF better in both halves). Paired by day: +240 pts, 7 days better / 6 worse, 90% bootstrap CI [+25, +497]. SENSEX PUT alone: CI [-38, +424], not significant.
- V1: fails (first-half net lower; skips 41% of trades; hurts control).
- V3: fails (no gain in second half).
- V2 on control: second half better, first half worse; not a consistent effect.
Today (11-11) V2 does not help on SENSEX (-9 vs +4).
Caveats: 16 SENSEX days, stops cluster by day, one dominant bad day (10-22 -341), 3 variants x 4 groups tested, live stop is a resting broker SL_M so V2 cannot be done there as-is.
