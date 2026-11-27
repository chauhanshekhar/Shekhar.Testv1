# Stop width with 5m ATR - R-multiple backtest

Replay: `--backtest-trades --regime-replay --from 2026-10-14`, live StrategySettings, Rs 20/order + 1 pt slippage.
Only `Atr5mStopMultiplier` was changed (via `--set`), stop = 5m ATR x multiplier, clamped to the 20-100 index-point range.
R = net P&L / (1R option points x lot size x lots). A scratch copy of the backtest (patched only to write 1R per trade)
was used; `src/` is unchanged. Numbers come from `summary.csv` / `by-symbol-side.csv` in this folder.

| 5m ATR multiplier | Trades | Win % | Net R | Expectancy | PF | Max DD | Net Rs | T1 reached | 1st half R | 2nd half R |
|---|---|---|---|---|---|---|---|---|---|---|
| 0.50 | 0 | - | - | - | - | - | - | - | - | - |
| 0.75 | 15 | 46.7 | +4.6 | +0.31 | 1.70 | 3.4 | +6,945 | 46.7% | +4.8 (6 tr) | -0.2 (9 tr) |
| **1.00 (live)** | 90 | 46.7 | **+21.3** | **+0.24** | **1.66** | **6.7** | **+48,333** | 44.4% | +2.4 | +18.9 |
| 1.25 | 118 | 38.1 | +9.4 | +0.08 | 1.15 | 7.9 | +18,549 | 32.2% | -2.5 | +11.8 |
| 1.50 | 112 | 39.3 | +14.3 | +0.13 | 1.27 | 7.5 | +34,017 | 30.4% | +0.8 | +13.4 |
| 1m ATR (5m stop off) | 1 | - | - | - | - | - | - | - | - | - |

By side (net R): live CALL +23.3 / PUT -2.0; 1.25 CALL +15.9 / PUT -6.5; 1.50 CALL +19.8 / PUT -5.6.

## Reading
- Of the multipliers with enough trades, 1.0 (live) is best on expectancy, profit factor, drawdown and net Rs.
  Widening to 1.25 / 1.5 adds 22-28 trades but they lower expectancy (+0.24R -> +0.08R / +0.13R), and T1 reach falls 44% -> 30-32%.
- The 0.50 and 0.75 results are NOT a test of tighter stops. `StopNoiseGate` (enabled live, ratio 1.0) blocks any trade whose
  index stop is smaller than the average 5m high-low range of the last 6 bars, so tighter multipliers lose almost every entry
  (0 and 15 trades). Testing them needs the gate switched off or its ratio lowered, a separate experiment.
- The entry set changes with the multiplier, so this compares stop-plus-gate configurations, not the same trades re-stopped.
- All variants lose most of their R in the first half (live +2.4R, 1.25 -2.5R); gains are concentrated in the second half.
  About six weeks of data; CALL carries the result, PUT is flat-to-negative everywhere.

## Not answered
- The effect of the 100-point cap (`MaximumIndexStopPoints`) on its own: the replay has no flag for it (it reads appsettings),
  so isolating it needs a small code change, which is waiting on approval. The 1.25 / 1.5 runs are the nearest proxy, since a
  larger multiplier pushes more entries into the cap.
- Whether today's three trades were cap-bound is inferred from their option risk / delta (about 100 index points each), not
  from the stored 5m ATR.
- The 1m-ATR variant gave 1 trade, so it says nothing (gates tuned for the 5m stop).

## Recommendation
Keep `Atr5mStopMultiplier` = 1.0; the data does not support widening. Nothing was changed in the database or code.
