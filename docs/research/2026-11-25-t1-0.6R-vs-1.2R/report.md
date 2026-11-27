# T1 at 0.6R vs current 1.2R (2026-11-25)

Question: should `AtrTarget1Multiplier` move from 1.5 (T1 = 1.5 / 1.25 = 1.20R) to 0.75 (T1 = 0.60R)?
Recommendation: **no - not supported.** 0.6R halves net profit and lowers profit factor in both halves.

## Method
- `Trading.Backtest --backtest-trades --from 2026-10-14`, live StrategySettings, Paper mode, live stop ladder,
  one open position across symbols/sides, OptionMinuteHistory premiums, costs included (Rs 20/order).
- Only difference between runs: `--t1-mult 0.75` (research-only override added to the backtest tool; live code unchanged).
- Window starts 10-14: T1 was already 1.20R on every CALL signal from then (verified in `Signals`), and a full-history
  run (from 07-10) times out loading snapshots.
- Outputs: `docs/research/2026-11-25-t1-base-1.2R/`, `docs/research/2026-11-25-t1-0.6R/`.

## Result (27 sessions, net of costs)
| | Trades | Win % | Net Rs | PF | Exp/trade | Max DD |
|---|---:|---:|---:|---:|---:|---:|
| T1 1.2R (current) | 89 | 47% | +52,610 | 1.76 | +591 | 14,588 |
| T1 0.6R | 94 | 59% | +23,763 | 1.34 | +253 | 15,741 |

- By half (split 11-03): first half 1.2R +9,690 (PF 1.31) vs 0.6R -3,051 (PF 0.90); second half +42,920 (PF 2.12) vs +26,814 (PF 1.68).
- By symbol: BANKNIFTY +20,763 -> +7,294; SENSEX +21,818 -> +13,365; NIFTY 3 trades.
- By side: CALL +53,323 -> +32,717; PUT -712 -> -8,954.
- 0.6R better on 4 of 27 days, worse on 19, equal on 4.
- Same-entry trades only (87 matched): +45,726 -> +19,977, so the loss is exit geometry, not different entries.
- Win rate rises (47% -> 59%) but winners are cut earlier: PROTECTED_SL_HIT 28 -> 37, TRAILING_SL_HIT 29 -> 23.
- Costs rise 17,040 -> 18,740.

## Caveats
- Current settings replayed over older days (settings that changed during the window are not restored).
- Premium path is one sample per minute; intra-minute dips are invisible. Earlier T1 arms the protected stop sooner,
  so 0.6R is the case more exposed to unseen dips - the real gap is likely larger, not smaller.
- 27 sessions, 89-94 trades: a clear direction, but a modest sample. Not tested: other T1 values (0.8R, 1.0R, 1.5R).
