# /live-stoploss-monitor analyze-streak (2026-10-28)

## A. Monitoring status
- **Mode:** `TradingMode` = **Paper**, `AutoTradingEnabled` = true.
  - No broker fills exist, so spread, real slippage and order latency are **Unavailable**.
  - Paper exits fill at the observed option LTP.
- **Data read** (read-only):
  - Signals and TradeJournal, summed per signal over all rows including partials.
  - Orders, IndicatorSnapshots (entry snapshot age < 1 s), MarketTicks (index, 1 s).
  - MarketTicks only holds 10-22..10-28, about 7 days of retention.
- **Window:** 103 closed signals, 2026-10-15..10-28 (9 days). Last processed: signal 10310, 10-28 11:57.
  - Net +9,204. 43 wins, 58 stop-loss losses.
  - Stop-loss loss = ExitReason `*_SL_HIT` **and** summed P&L < 0, not P&L alone.
- **Not running:** continuous monitoring. This is a one-off analysis of stored records.

## B. Streaks (≥ 2 consecutive stop-loss losses)
- There were 15 streaks in 9 days. The largest:
  - 10-22: 8 trades, -13,036
  - 10-26: 5 trades, -10,770 (analysed below)
  - 10-15: 5 trades, -9,430
- The most recent: 10307-10308 on 10-28, -2,806. 10308 lost only Rs 76.
- Full list: `streaks.py` output.

## C. 10-26 BANKNIFTY streak, trade by trade (`streak-1026.csv`)
| Id | Dir | Entry→Exit | Held | Net | Exit | Rsi1m/5m | 1m EMA | 5m EMA20 vs 50 | Dist EMA20 (ATR) | Idx stop pts (ATR) | Opt MFE |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 10285 | PE | 10:40:00→10:40:45 | 0.8 m | -3,000 | OPTION_SL | 31.9/45.3 | bear | bear | -2.5 | 62 (1.25) | +1.3% |
| 10286 | CE | 12:35:04→12:38:38 | 3.6 m | -3,012 | OPTION_SL | 60.4/55.2 | bull | **below** (-30) | +1.25 | 43 | +3.1% |
| 10287 | CE | 13:20:01→13:20:10 | 9 s | -1,602 | PROTECTED_SL | 64.7/56.5 | bull | below (-21) | +1.66 | 31 | -0.1% |
| 10288 | CE | 13:37:04→13:38:14 | 1.2 m | -690 | PROTECTED_SL (breakeven) | 59.2/55.0 | bull | below (-7) | +1.18 | 40 | +5.3% |
| 10289 | CE | 13:54:00→13:56:34 | 2.6 m | -2,466 | OPTION_SL | 58.3/54.8 | bull | below (-1.5) | +1.12 | 37 | +0.7% |

- **Shared facts:**
  - Same symbol, Normal open (about -0.1%).
  - The four CALLs are all in MarketState Scalping, RSI5m 55-56, 1.1-1.7 ATR above EMA20.
  - The index entries were at 54105, 54109, 54153 and 54155: the **same level, re-bought 3 times within 35 min**, with no new break above it.
- **5m EMA20 < EMA50 on every CALL:** this is expected, not a fault.
  - EarlyTrendGate (enabled 10-08) only allows a CALL while the 5m EMA20 is not above the EMA50.
  - The 10284 winner the same morning had the same conflict and made +5,718.
- **Primary classification:**
  - 10285, 10286, 10289: `StopDistanceMismatch`/`RangeOrChop` (Medium). The stop was hit within 1-4 min and the option never gained more than 3%.
  - 10287: **`StaleOrMissingData` / `StopImplementationMismatch` (High)**, see D1.
  - 10288: the breakeven stop was armed at the entry price, but the paper exit filled 5.75 points lower (`Slippage`, paper fill on a gap between ticks).

## D. Root causes, ranked
### D1. Bad index ticks equal to yesterday's close move the exit logic — **High, confirmed defect**
- **10287:** at 13:20:10 a single BANKNIFTY tick printed 54287.35, which is exactly the previous close. The ticks either side were 54137 and 54126. There was also a 10 s gap in the tick feed just before it.
- **What the code did:** `ExitDecisionService.cs:193-210` treated that tick as reaching index T1 (54108.95+). It set `IndexT1Hit`, armed `ProtectedSL` = entry × 0.97 = 347.75, and the option at 345.15 was immediately below that.
- **Result:** the trade exited 9 seconds in, at -1,602. Without that tick its option stop would have been 339.90. The outcome after that point is unknown, because premium history after the exit was not checked.
- **Scale:** across 10-22..10-28 there were **41 one-tick index spikes** (> 0.15% against both neighbours).
  - **26 of them equal the previous close exactly**, on all three indices (`tick-spikes.csv`).
  - 4 of 70 trades had an exit or a state change within 2 s of a spike (`spike_hits.py`): 10287 armed its protected stop; 10267, 10294 and 10305 reached the index stop.
- **Same pattern in `IndicatorSnapshots.DayOpen`:** for BANKNIFTY on 10-26 it changes during the day (54250.30 → 53872.90 → 54029.20 → 53960.35). This would also corrupt any live gap-regime logic.

### D2. Stops sit inside 1-4 minute noise — **Medium, already known**
- 37 of 58 stop-loss losses hit within 2 min. The median losing trade's option peaked only +3%.
- Losers and winners look the same at entry:

| Entry feature (median) | Stop-loss losses (58) | Winners (43) |
|---|---|---|
| RSI5m | 37.4 | 38.0 |
| Distance from EMA20 (ATR) | 1.30 | 1.45 |
| ATR% | 0.06 | 0.06 |
| Stop size (ATR) | 1.25 | 1.25 |

| Entry condition | Losses | Winners |
|---|---|---|
| 5m EMA with the trade | 37/58 | 26/43 |
| Re-entry within 1 ATR of the previous same-direction entry | 16/58 | 11/43 |
| Re-entry after a stop-loss | 24 | 19 |

- This matches the stored findings `why-stops-measured`, `entry-confirmation-delay-rejected` and `stop-width-tested`.
- Losses cluster by day: 10-22 had 19 stops on 26 trades. No new entry rule is supported.

### D3. Re-buying the same level (`RepeatedReentryWithoutFreshSetup`) — **Low**
- It is visible on 10-26, but across the window it is as common among winners (11/43) as among losses (16/58). That is not enough to act on.

## E. Next tests (max 3)
1. **Filter bad index ticks before the exit logic** (defect fix; code change, needs approval).
   - Hypothesis: one-tick prints equal to the previous close are feed fallbacks.
   - Rule to test: ignore an index tick that equals `YesterdayClose`, or jumps > 0.15% and reverts on the next tick, for T1/SL purposes.
   - Pass if: in a replay of the 7 days of ticks, 0 spike-triggered arms or exits, and nothing changes on the other 66 trades.
2. **Trace where `DayOpen` and the fallback LTP come from** in the Groww quote mapping.
   - Read-only investigation of what the quote returns when LTP is missing.
   - Pass if: the root cause is found and DayOpen stays constant within a session.
3. **No strategy test.** Stops hit fast and are not predicted by any entry feature on 101 trades. Earlier rejections cover stop width, cooldown and confirmation delay.

## F. Action
- **Run a replay or fix:** D1 is a data defect, not a strategy problem. Fixing it is a code change in `src/` and needs your go-ahead.
- **Review risk limits:** 15 streaks in 9 days. ConsecutiveLoss, MaxDailyTrades and CircuitBreaker are off for paper by your choice. Restore them before Live.
- **Keep collecting evidence:** on everything else.
- No live setting or trade was changed.

Files: `db.py`, `streaks.py`, `streak_context.py`, `bad_ticks.py`, `spike_hits.py`, `compare.py`, and the outputs
`closed-signals.csv`, `streak-1026.csv`, `tick-spikes.csv`, `trades-features.csv`.
