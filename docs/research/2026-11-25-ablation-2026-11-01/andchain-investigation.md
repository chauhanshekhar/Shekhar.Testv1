# AndChain parity misses (2026-11-01 window) - investigation notes

Read-only. No code, settings or data changed. Source: parity/signal-parity.csv (68 LIVE_ONLY rows blocked by AndChain).

## What the 68 are
- 67 fail AND-chain step 4 (previous-5m high / low). 1 fails step 2.
- 32 CALL (LTP > Prev5mHigh + 5), 36 PUT (LTP < Prev5mLow). All 68 are dated before 2026-11-21.

## CALL (32): explained
SignalEngineService.cs step 4 has `const decimal buffer = 5m` ("Compare 5 vs 10 in replay"), added in commit 4d50faa (2026-11-21).
The replay runs today's code on old days, so CALLs live took before 11-21 are rejected for lacking the +5 buffer.
Checked: signal 10318 BANKNIFTY CALL 2026-11-02 14:33:04 - live Ltp 54746.75; last closed 5m high (stored Candles BANKNIFTY_5m 08:55 UTC) 54745.15.
Old rule (Ltp > high) passes; current rule (needs > 54750.15) fails.

## PUT (36): NOT explained
No buffer on the PUT rule. Three PUTs checked against stored 5m candles:
| Signal | Live Ltp | Replay Prev5mLow | Live Ltp - Prev5mLow |
|---|---:|---:|---:|
| 10319 BANKNIFTY 11-03 12:19:00 | 55458.30 | 55407.90 | +50.4 |
| 10323 SENSEX 11-03 14:26:05 | 76574.74 | 76559.07 | +15.7 |
| 10324 BANKNIFTY 11-04 11:55:00 | 55464.85 | 55450.30 | +14.6 |
In all three live passed `Ltp < Prev5mLow` although Ltp was ABOVE the stored previous-5m low (also above the following bar's low for 10319).
So live's Prev5mLow at decision time was higher than the stored candle's low. Hypotheses (unverified): live saw a partial/forming or lagged 5m bar
(10319 fired 0.7 s after the minute boundary), or 5m candles were revised after the fact. Needs a check of how Candles *_5m rows are written/updated.

## Not yet done
- 49 replay-only entries blocked live by SignalEngine (same step-4 area, opposite direction) not examined.
- Only 3 PUTs sampled of 36.

## Practical consequence
Parity before 2026-11-21 is expected to be poor for CALLs by construction. From 2026-11-25, 3 of 3 reproduced. Use a window starting on/after 2026-11-21 (CALL rule) and 2026-11-24 (new gates).
