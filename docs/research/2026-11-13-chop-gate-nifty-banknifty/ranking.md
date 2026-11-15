# Chop gate (15 bars, block > 4.5) on NIFTY / BANKNIFTY real trades (2026-11-13)
Script analyze.py (read-only), run.log, signals.csv. Real TradeJournal P&L per signal, gross of costs, no look-ahead, HeroZero exempt, <15 bars passes.
| Symbol | n | blocked (share) | blocked net / PF | kept net / PF | H1 blocked | H2 blocked |
|---|---|---|---|---|---|---|
| NIFTY | 31 | 5 (16%) | -8,385 / 0.17 | +16,372 / 1.67 | -2,418 (n1) | -5,967 (n4) |
| BANKNIFTY | 127 | 20 (16%) | **+12,950 / 1.90** | +3,936 / 1.03 | +7,177 (n9) | +5,774 (n11) |
| SENSEX (ref) | 142 | 14 (10%) | -11,302 / 0.05 | +12,272 / 1.15 | -7,192 (n10) | -4,110 (n4) |
## Reading
- BANKNIFTY: the gate would REMOVE profit. Blocked trades are the better ones in both halves, in CALL and PUT. NOT SUPPORTED; harmful.
- NIFTY: blocked trades lose, but n=5 on 4 days (2 trades on 10-28 = -5,174, one blocked trade on 11-03 was +1,677). Too thin to call; consistent sign only.
- SENSEX: same-sign both halves with the exact live rule, but the 4.5 cutoff was picked on this data (in-sample).
Caveats: gross P&L; the blocked set is chosen by the cutoff; stops cluster by day; real trades only (already filtered by live rules), not an unfiltered base rate.
