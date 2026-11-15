# SENSEX index-noise entry gate (2026-11-12)
Script analyze.py (read-only), run.log, signals.csv. 141 real SENSEX trades with journal P&L (gross of costs), 2026-08..11-12.
Features from SENSEX_1m candles closed before entry: chop = sum(1m ranges, 15 bars)/(15-bar high-low); rng_s = mean 1m range / index stop distance.
Today (11-12): 12 signals, 9 stopped, many inside 1 min of entry; 3 of the 4 worst (-2434, -1572, -1376) had chop 4.5-4.8 (top decile is >4.5).
## Result: NOT SUPPORTED (fails both-halves rule). Do not implement.
- chop high tercile: all -10,207 (PF 0.68) but H1 +746 (PF 1.05), H2 -10,954. Low tercile also flips (H1 +16,349, H2 -9,522).
- H1-fit cutoffs: q80 blocks H1 +567 (wrong side), H2 -5,750 on n=6; q90 H2 n=3. Too few trades.
- rng_s (stop vs 1m range): high tercile is BEST in H1 (+8,886) and still less bad in H2; opposite of a noise gate.
- Option premium is only minute-level (no option ticks stored); index ticks are kept 7 days only.
Caveat: P&L gross; stops cluster by day; ~10 cutoffs tried.
