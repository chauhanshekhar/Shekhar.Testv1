# Gap-up PUT: base-rate check of 35-60 vs live 40-60, 2026-11-04

`--base-rate --side PUT --cost-per-order 20 --slippage-points 1 --dump-trades` (every entry filter and queue limit off), then rows with OpenGapPct >= 0.30 bucketed by 5m RSI.
376 gap-up rows over 24 sessions. Rows are per decision minute, so they overlap; read per-trade averages and PF, not n, as evidence. Net after costs.

| 5m RSI | n | days | net/trade | win % | PF |
|---|---|---|---|---|---|
| 20-28 | 13 | 5 | +1,253 | 69.2 | 2.94 |
| 28-35 | 41 | 10 | -104 | 39.0 | 0.87 |
| **35-40** | 63 | 14 | **-367** | 38.1 | **0.64** |
| 40-45 | 82 | 20 | -41 | 42.7 | 0.94 |
| 45-50 | 69 | 23 | +318 | 42.0 | 1.46 |
| 50-55 | 59 | 23 | -56 | 42.4 | 0.94 |
| 55-60 | 29 | 13 | +629 | 62.1 | 2.33 |
| 60-65 | 13 | 8 | -621 | 23.1 | 0.44 |
| 40-60 (live) | 239 | 24 | +140 | 44.8 | 1.19 |
| 35-60 (candidate) | 302 | 24 | +35 | 43.4 | 1.04 |

35-40 bucket by half: first half PF 0.77, second half PF 0.58. By symbol: BANKNIFTY PF 0.55, SENSEX PF 0.75, NIFTY no rows.
Live 40-60 by half: first half PF 0.90, second half PF 1.41; BANKNIFTY PF 1.72, SENSEX 0.90.

## Verdict
The extra trades 35-60 adds (the 35-40 bucket) lose on the unfiltered base rate in both halves and both symbols. The +13k in the filtered run is not supported: **35-60 REJECTED**. Live 40-60 stays. The 40-60 edge itself is mostly BANKNIFTY and the second half; SENSEX is flat to negative.
