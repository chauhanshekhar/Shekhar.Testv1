# Data-quality audit

- Aligned outcome rows: 1,087,364 of 1,087,364 (0 orphans) — alignment check passed.
- Deduplicated to one per symbol-minute-direction: 23,813 rows (CALL 3,651 / PUT 3,697 in base after window rules). Unresolved (NULL) outcomes: 0.
- Date range: 2026-09-30..2026-11-03, 23 trading days. Split by day: train 13 / val 5 / holdout 5 (~57/22/22, chronological).
- Missing features: Delta 87.3%; all others 0%. Delta bands are effectively untestable.
- Limitations: holdout is only 5 days; outcomes are index-point barriers, not option premium; pre-09-30 13:30 5m indicators were 1m-lagged (see memory) — range starts 09-30 so partially affected; overlapping 15-min windows within a day mean observations are not independent.
