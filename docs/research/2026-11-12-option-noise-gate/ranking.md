# Option noise entry gate (2026-11-12)
Script: analyze.py (read-only), log: run.log, per-signal features: signals.csv.
195 real signals 2026-10-07..11-11 (25 sessions of OptionMinuteHistory). Outcome = baseline bracket replay (stop, T1, 15:15, 2 pts cost); NOT live P&L.
Features at entry: spike = entry LTP / median(last 5 completed minute LTPs) - 1; noise = median |pct change| over last 5 minutes.

## Result: NOT SUPPORTED as proposed. Do not implement.
- The "spike > 2% above median" rule blocks 121 of 195 trades (62%). Median spike is 3-4% on every index: it measures an up-trending premium (momentum), not quote noise.
  Blocked vs kept flips sign between halves (SENSEX H1 blocked +81 vs kept -84; H2 blocked -194 vs kept +76).
- Out-of-sample noise cutoff (fit on H1, apply to H2): blocked trades lost in H2 at q=10%/20%, but the same cutoffs on H1 did not separate (SENSEX q20 H1 blocked exp +4.85). BANKNIFTY drives the H2 gain; SENSEX q30 H2 blocked were profitable.
- Noise vs stop rate: NIFTY/BANKNIFTY low tercile better, SENSEX not monotonic (45% stop in the high tercile vs 38% mid).
- Day level (SENSEX): corr(day noise, day net) = -0.06. 10-08 (noise 4.9%, +333) and 10-22 (noise 6.5%, -341) look alike.
- Percent noise is not normalised for premium level; cheap options have large % moves from one tick.
Caveats: 195 trades, stops cluster by day (16 SENSEX days), minute sampling hides tick spikes, many cutoffs tried.
