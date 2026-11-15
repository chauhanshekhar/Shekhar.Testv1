# Pasted CALL hypotheses A/B/C (2026-11-05)
Script: test_pasted.py (reads 2026-10-26 snapshot/outcome export, candles-1m.csv). Output: cells.csv.
Data 07-29..10-26, 43 days; split train <09-29, val to 10-13, holdout after. Rs40 round-trip cost, no slippage.
Result: every cell net-negative in holdout (PF 0.23-0.90 vs base 0.56). Classification: FAILED / INSUFFICIENT DATA for A.
- A: only 11 GapUp days; VWAP exists on 2 of them (VWAP logged from 10-09) -> untestable. A without VWAP: 8 days, PF 0.49, holdout 6 trades.
- B: best-looking (win 55%, ExpR +0.12R) but net -107k, holdout PF 0.80. Positive R with negative Rs = losses larger than wins after sizing/costs; not an edge.
- C: Put/Call OI change is not stored per snapshot -> untested. PCR delta only: low-open & rising PF 0.48 (worse), falling PF 0.82 (still <1), small n.
Not tested: stored supertrend (computed here from 1m candles, ATR10 x3), per-week breakdown, execution parity (stage 2).
