# Whipsaw shadow gate - historical result (2026-11-18)

Command: `dotnet run --project src/Trading.Backtest -- --whipsaw --side BOTH --cost-per-order 20 --slippage-points 1 --out docs/research/2026-11-18-whipsaw-gate`
Data: NIFTY/BANKNIFTY/SENSEX 1m+5m candles, 78-91 sessions. Simulated, costed P&L; same scoring code as the live gate. Full tables: whipsaw.md / whipsaw.csv.
Limit: removed trades do not free the one-position slot (not re-simulated). Outcomes TARGET = reached T1.

## Result: NOT SUPPORTED for blocking
- **Severe (score 3) is untestable**: fires on 2 of 1,888 CALL and 0 of 1,977 PUT unfiltered-base trades (1m EMA flips >= 3 almost never happens: 13 times). Blocking it changes nothing.
- **Caution+Severe (score >= 2) on PUT is harmful**: blocks trades worth +81,508 (base; 120 target / 129 SL), +19,237 on live-rule PUTs. Same sign in both date halves and on BANKNIFTY / SENSEX / NIFTY base.
- **On CALL it is inconsistent**: base blocked -49,768 overall, but second half of dates +3,274 (hurts); live-rule BANKNIFTY CALL +423 (no help); SENSEX CALL live -16,697 (helps, n=32). 1m/5m disagreement is true on 226/229 live CALL trades (the early-trend setup is a disagreement by construction), so score 2 is mostly "5m candles alternated".
- Driver is the 5m-alternation signal (~30% of trades), a weak chop proxy, consistent with the earlier rejected chop/noise filters.

## Recommendation
Do not enable blocking. Leave the shadow gate off or on (log only, no effect). Re-test after ~10 sessions of WHIPSAW_SHADOW logs, and only if VWAP-cross / ATR-percentile are added.
