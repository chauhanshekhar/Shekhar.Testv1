# Sideways filters: range-vs-stop (F1) and no-new-extreme (F2) - NOT SUPPORTED (2026-11-13)
Script analyze.py (read-only), run.log, signals.csv. 300 real journal trades, HeroZero excluded, costed Rs40 + qty x 1pt, same-session closed 1m bars, fail-open without history.
F1 = span of last 30/60 bars / index stop distance, block when LOW (cutoff = H1 quantile 10/20/30%). F2 = minutes since session low (PUT) / high (CALL) >= N (15/30/45/60, fixed). ~40 cells searched.
## Result
- F2: every symbol and the pooled set lose in H1 from blocking (pooled -7k to -12k) and gain in H2 (+11k to +16k): sign flips between halves again. By opening type blocking hurts GapUp (-5k to -12k) and helps Normal (+8k to +11k) and GapDown (N>=30). By symbol/type it contradicts itself (BANKNIFTY PUT +14.3k, SENSEX PUT -11.5k, SENSEX CALL +9.0k at N=30). Only SENSEX N=60 passes both halves (H1 +1.7k, H2 +5.9k).
- F1: BANKNIFTY range30/stop<q30 passes both halves only because H1 is flat (+4) while H2 is +18.9k (15 trades, 6 of 8 days helped); tighter cutoffs q10/q20 LOSE in H1 (-9.3k/-8.4k). SENSEX F1 is the opposite (H1 +5k to +12k, H2 negative). Contradicts across symbols/halves.
- Today BANKNIFTY: F2 N=60 would have blocked 7 of 13 trades (gross +5.5k avoided), but that is one day and F2 fails the cross-half and cross-symbol check.
Caveats: real trades only; stops cluster by day; ~40 cells searched; cutoffs chosen on H1; costs approximate.
## Verdict: do not implement either. Re-test BANKNIFTY F1 range30/stop (q30) after ~10 more sessions - only item worth watching.
