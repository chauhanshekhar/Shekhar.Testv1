# Long-window chop entry gate (2026-11-13) - NOT SUPPORTED
Script analyze.py (read-only), run.log, signals.csv. 300 real journal trades (NIFTY 31, BANKNIFTY 127, SENSEX 142), HeroZero excluded, costed at Rs40 + qty x 1pt.
chop_W = sum(1m ranges, last W closed same-day bars) / (W-bar high-low); W in 15/30/45/60/90; cutoff = H1 quantile (70/80/90%) applied to H1 (in-sample) and H2 (out-of-sample). ~75 cells searched.
## Result
- BANKNIFTY: blocking would have LOST money in H1 in all 15 cells (-1.7k to -20.6k costed) while H2 gains are positive for W>=30 (+1.9k to +17.8k). Sign flips between halves; no BANKNIFTY cell passes both halves. Today's stops are in H2.
- Cells positive in both halves with >=8 H2 blocks: NIFTY 15/70 (H1 +772), SENSEX 15/70, SENSEX 90/70 and 90/80 (H1 +79), pooled 15/90 (H1 +1.1k), pooled 90/70 (H1 +367, H2 +7.2k, 28 blocked). H1 effects are ~zero; the gain is all H2, and days blocked-and-positive are 4 of 14 (pooled 90/70).
- NIFTY W60 passes both halves but with 4 H2 trades on 3 days. Too thin.
- Today BANKNIFTY (13 trades): 60-bar chop 5.5-10.3, 90-bar 6.4-11.4 on the 9 trades with data; the earliest trades have no same-day window, so a long window cannot block the morning entries at all.
Caveats: real trades only (already pass live rules); stops cluster by day; cutoffs searched; window excludes the first W minutes of the session.
## Verdict: do not implement. Re-test BANKNIFTY and the pooled 90-bar rule after ~10 more clean sessions.
