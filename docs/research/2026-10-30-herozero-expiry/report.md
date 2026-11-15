# HeroZero on expiry days — why late, and would earlier/looser help? (2026-10-30)

Script: `study.py` (read-only). Outputs: `real-trades.csv`, `threshold-first-cross.csv`.

## Why it fires late (code)
- Expiry day: StrategySelector routes to Directional until `ExpiryGammaBlastStartTime` = 14:00 (TradingSettings.cs).
- From 13:30 HeroZero uses lottery mode: premium 5-20, score >= 70 (HeroZeroSignalEngine.cs).
- 10-29 SENSEX: best CE scored 63 from 14:02 (momentum 8/..), reached 70 only at 14:57.

## Real HeroZero trades (all history, TradeJournal, cost Rs20/order x2 + 1pt x qty)
14 journal rows (13 signals), 3 wins, net **-510**. All profit came from one day (NIFTY 10-13, +7.8k).

## Lower score threshold after 14:00 (OptionMinuteHistory, 8 expiry days since 10-07)
First option per day to reach the threshold; outcome = 2x or -30% premium first.

| Threshold | Days entered | 2x first | -30% first |
|---|---|---|---|
| 60 | 8 | 1 | 7 |
| 63 | 8 | 0 | 8 |
| 65 | 6 | 0 | 6 |
| 70 (live) | 5 | 1 | 4 |

10-29 at score 63 (14:02, 74500CE @ 8.95): fell to 6.20 (-31%) before peaking at 17.25 — the earlier entry would also have been stopped.

## Earlier handover (before 14:00)
**Not testable**: HeroZeroScores are only computed from 14:00, so no stored score exists earlier. Testing it needs
the scorer re-run on OptionMinuteHistory (history since 10-07 only, ~8 expiry days).

## Conclusion
No change recommended. Lowering the score adds losing entries; HeroZero's whole result rests on one day.
Sample is tiny (8 expiry days) — revisit after ~10 more expiries.
