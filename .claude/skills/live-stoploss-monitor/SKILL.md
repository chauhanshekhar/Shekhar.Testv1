---
name: live-stoploss-monitor
description: Monitor live Indian F&O market conditions and actual CALL/PUT trades, investigate consecutive stop-loss hits, correlate executions with market snapshots, and recommend evidence-backed fixes.
---

# Live Stop-Loss Monitor

## 1. Objective

Identify the actual reasons stop-losses are hit repeatedly in the automated Indian F&O trading system.

Monitor actual executed trades and their corresponding market conditions to answer:

- Why did this specific trade hit stop-loss?
- Was the entry valid according to the strategy rules at that time?if valid strategy why stoploss hit ?
- Did the underlying price confirm a breakout or reverse immediately?
- Were 1-minute and 5-minute trends aligned?
- Was the entry late, extended, or taken during sideways movement?
- Did option-premium movement, spread, slippage, or execution latency contribute?
- Was the stop-loss too close relative to actual volatility?
- Was a trade re-entered without a fresh level break?
- Which conditions occur more frequently in losing trades than in comparable winning trades?
- Which potential fix improves results in a realistic replay?

The skill must diagnose using actual evidence, not guesses based on a few losing trades.

**Do not place trades or change live strategy configuration automatically.**

## 2. Commands

Support these commands:

- `/live-stoploss-monitor` — inspect available monitoring data and recent actual trades.
- `/live-stoploss-monitor analyze-last 5` — investigate the five most recent actual trades.
- `/live-stoploss-monitor analyze-streak` — analyze consecutive stop-losses and compare them with recent winners.
- `/live-stoploss-monitor today` — investigate today's trades.
- `/live-stoploss-monitor compare-stoploss-winners` — compare losing trades against comparable winners.
- `/live-stoploss-monitor replay` — test identified causes against the established backtest engine.
- `/live-stoploss-monitor status` — report data freshness, last processed trade, and missing sources.

If the live market feed or brokerage execution data is not accessible, say so explicitly. Analyze available records without claiming that continuous monitoring is running.

## 3. Data to collect

Discover the actual repository, database schema, market feed, and execution logs before assuming table or column names.

### A. Actual trade execution

Collect, when available:

- Trade, order, position, and strategy IDs.
- Symbol, underlying, expiry, strike, CE/PE, direction, quantity, and lot size.
- Signal timestamp, order submission time, acknowledgement time, and fill time.
- Actual entry price and average fill price.
- Configured stop-loss, target, exit trigger, exit reason, and actual exit fill.
- Gross P&L, brokerage/fees, estimated slippage, and net P&L.
- Re-entry count, cooldown status, and shared-position/slot state.
- Whether a fresh setup or level break occurred before re-entry.
- Partial fills, rejected orders, cancelled orders, and modified orders.

Do not classify a trade as a stop-loss exit from negative P&L alone. Verify the exit reason from the execution logs or stop trigger evidence.

### B. Market and indicator data

Capture the latest valid values at entry and around the stop-loss:

- Underlying price and option premium.
- Day open and previous close.
- Opening gap percentage.
- EMA20 and EMA50 on 1-minute candles.
- EMA20 and EMA50 on 5-minute candles.
- RSI1m and RSI5m as separate fields.
- ATR raw and ATR%.
- PCR, VWAP, trend direction, market regime, and market state, where available.
- Strategy selected, signal generated, and all relevant rule pass/fail results.
- Last completed candle OHLCV and current candle data where available.
- Breakout level, support/resistance level, and confirmation state if available.
- Option bid/ask, spread, volume, depth, implied volatility, and Greeks where reliable.
- Indicator timestamp, source timestamp, snapshot timestamp, and data freshness.

The known strategy has used 1-minute candles for entry timing and 1-minute EMA20/EMA50, with 5-minute EMA20/EMA50 as a trend filter. Verify the current implementation before assuming these rules remain unchanged.

Do not invent VWAP when volume is unavailable.

## 4. Capture the timeline around each trade

For every actual entry, reconstruct the market state using snapshots at these relative times, where available:

| Time | What to investigate |
|---|---|
| T−5 min | Trend, gap regime, volatility, recent highs/lows, and whether the move is extended |
| T−3 min | Setup formation, EMA alignment, RSI behavior, and nearby levels |
| T−1 min | Last completed candle, rejection wick, pullback, and breakout confirmation |
| Entry | Signal values, actual fill, spread, slippage, stop distance, and rule results |
| T+1 min | Immediate follow-through or adverse movement |
| T+3 min | Retest, reversal, EMA/RSI changes, and adverse excursion |
| T+5 min | Whether the original trade thesis remains valid |
| Stop exit | Stop trigger, underlying movement, premium movement, latency, and actual exit price |

If the stop can trigger intrabar, use tick/quote data where available. If only candle data exists, state that the diagnosis has candle-level limitations.

Never use future data to explain what the system knew at entry time.

## 5. Stop-loss classification

Give every stop-loss one primary classification and optional secondary classifications. Use `Unknown` if evidence is insufficient.

### A. Entry quality

- `NoConfirmedBreakout` — entry without required breakout or level confirmation.
- `FalseBreakout` — price crosses a level but quickly reverses.
- `LateEntry` — entry occurs after much of the move has already happened.
- `PullbackEntryFailure` — entry is taken during a pullback before continuation is confirmed.
- `ChaseAfterImpulse` — entry follows an extended candle without adequate continuation evidence.
- `ExhaustionOrReversal` — price reverses after an extended move; requires supporting evidence.

### B. Trend and market regime

- `EMAConflict1m5m` — the entry timeframe and 5-minute trend disagree.
- `CounterTrend` — trade opposes the verified broader direction.
- `RangeOrChop` — repeated direction changes and weak follow-through.
- `GapRegimeMismatch` — performance is poor in the observed gap regime, supported by adequate data.
- `LowVolatility` — insufficient movement relative to the trade's costs and risk.
- `HighVolatilityWhipsaw` — unstable moves repeatedly cross entry levels.

### C. Indicator problems

- `RSIContextMismatch` — RSI context is associated with poor continuation in comparable trades.
- `ATRFilterMismatch` — ATR filtering fails to distinguish tradable movement from noise.
- `EMAGapTooSmall` — EMA separation is below the verified strategy threshold.
- `VWAPUnavailableOrProxy` — VWAP is missing or only a proxy was used.
- `StaleOrMissingData` — required data is unavailable or stale.
- `FeatureTimingMismatch` — live strategy, stored snapshots, and replay use inconsistent timestamps or indicator definitions.

### D. Option pricing and execution

- `SpreadOrLiquidity` — spread or market depth plausibly contributes to the loss.
- `Slippage` — actual fills materially differ from expected prices.
- `OrderLatency` — delayed submission or execution affects the exit.
- `PremiumUnderlyingDivergence` — option premium behaves differently from what the underlying movement alone would suggest.
- `PremiumDecayOrIVChange` — premium loss is associated with time value or implied volatility, but only if data supports this conclusion.
- `StopImplementationMismatch` — actual stop execution differs from configured logic.

### E. Re-entry and risk management

- `RepeatedReentryWithoutFreshSetup` — a new trade occurs without the required fresh setup or level break.
- `CooldownOrSlotMismatch` — actual position state differs from intended cooldown or slot logic.
- `StopDistanceMismatch` — stop distance may be inappropriate for observed volatility, requiring comparative testing.
- `PositionSizingOrExposure` — position size or correlated exposure increases the loss.

A classification is a hypothesis until supported by event-time data and comparison with other trades.

## 6. Consecutive stop-loss incident analysis

When two or more stop-losses occur consecutively, generate a `StopLossStreakIncident` report.

For each trade in the streak:

1. List entry time, exit time, symbol, direction, entry price, stop level, exit price, net P&L, and time to stop.
2. Reconstruct market and strategy state at entry and exit.
3. Identify shared features across the losing trades.
4. Compare those features against recent winners and other comparable trades.
5. Determine whether the losses are independent setups or repeated exposure to the same market movement.
6. Check whether repeated entries occurred without a fresh level break.
7. Measure how quickly the stops were hit and whether the original thesis ever showed follow-through.
8. Separate confirmed facts from suspected causes.
9. Rank the likely causes by evidence strength.
10. Propose a small number of tests to distinguish the leading explanations.

Investigate whether the trades share:

- Symbol and direction.
- Opening-gap regime.
- Session time.
- EMA alignment and EMA separation.
- RSI1m and RSI5m.
- ATR%.
- Breakout level and candle structure.
- Entry distance from EMA20.
- Time since the previous stop.
- Re-entry status.
- Spread, slippage, and order latency.

Do not conclude that a strategy is broken solely because of a short losing streak. However, explicitly flag the streak and suggest reviewing the user's pre-defined risk limits.

## 7. Compare stop-losses with winners

Do not analyze losing trades in isolation.

Compare stop-losses with winners and other exits matched as closely as possible by symbol, direction, time of day, market regime, setup, and contract characteristics.

Analyze:

- RSI1m versus RSI5m distributions.
- 1-minute versus 5-minute EMA alignment.
- EMA gap and LTP distance from EMA20.
- ATR% and favorable/adverse price excursion.
- Opening gap versus `PriceChangePct`, keeping them separate.
- Time from signal to entry and entry to stop.
- Breakout/level confirmation.
- Spread, slippage, and order latency.
- Re-entry status and fresh-breakout confirmation.
- Stop distance relative to recent price movement.

For each feature, report:

- Number of stop-loss trades.
- Number of comparable winning trades.
- Frequency in both groups.
- Effect size and uncertainty where feasible.
- Date coverage and sample size.

A condition found in many losing trades is not necessarily a cause if it is also common in winning trades.

Avoid selecting a filter from many tests without accounting for data-mining risk.

## 8. Data integrity

- Use actual execution timestamps and fills for real trades.
- Match market snapshots to the nearest valid snapshot at or before the event.
- Keep source timestamps and ingestion timestamps separate.
- Normalize timezones consistently.
- Verify symbol, expiry, strike, option type, and underlying.
- Detect stale, duplicated, missing, or out-of-order records.
- Do not use a future snapshot to explain an entry.
- Do not blindly join outcomes to snapshots through a key known to be misaligned.
- Keep actual closed-trade results separate from simulated outcomes.
- Label conclusions as unknown when supporting data is missing.

If feature/outcome alignment cannot be verified, stop the affected analysis and explain the limitation.

## 9. Live monitoring and alerts

If the required feed and notification integrations are actually available, monitor for:

- A new actual stop-loss exit.
- Two or more consecutive stop-losses.
- Re-entry without a required fresh setup.
- Missing or stale critical market data.
- A mismatch between configured and executed stop behavior.
- Repeated order rejection, abnormal slippage, or execution latency.
- A material mismatch between live signals and recorded snapshots.

Group related events into an incident to avoid repeated noisy alerts.

Each alert should include event time, symbol/direction, relevant trade reference, supporting evidence, and the suspected issue.

If live feeds are unavailable, do not claim to be monitoring continuously. Report the latest data available and what connection is missing.

Do not automatically pause, resume, modify, or place trades. If a streak occurs, recommend that the user review their pre-defined risk limits and consider manual review before further entries.

## 10. Test fixes before deployment

Do not change live rules based on a few stop-losses alone.

For each proposed fix:

1. State a specific hypothesis.
2. Identify the exact rule or feature to test.
3. Change one primary factor at a time where practical.
4. Replay the same dates and instruments with identical costs, slippage, entry/exit rules, and position constraints.
5. Compare the candidate with the baseline.
6. Use chronological out-of-sample or walk-forward validation.
7. Check whether improvement comes from better trade quality or merely fewer trades.
8. Compare simulated decisions with actual execution logs.
9. Report supporting evidence, counter-evidence, and remaining uncertainty.
10. Require explicit user approval before any live configuration change.

Measure at least:

- Net P&L after costs.
- Profit factor and expectancy.
- Drawdown.
- Number of trades.
- Stop-loss count and consecutive stop-loss streaks.
- Average time to stop.
- Average win and average loss.
- Missed winning trades.
- Stability across days, symbols, and market regimes.

Confirm the current brokerage, fees, slippage, and execution assumptions before reusing older backtest settings.

## 11. Required report

### A. Monitoring status
- Latest data timestamp and freshness.
- Actual trade data available.
- Latest trade processed.
- Missing or disconnected sources.

### B. Stop-loss streak
- Consecutive stop-loss count.
- Symbols and directions.
- Entry/exit times and net loss.
- Whether the trades share a common setup or market condition.

### C. Trade-by-trade evidence

Include, where available:

- Trade and timestamps.
- Symbol and direction.
- Entry, stop, and exit prices.
- Net P&L and time to stop.
- RSI1m and RSI5m.
- EMA20/EMA50 on 1-minute and 5-minute timeframes.
- EMA separation and ATR%.
- Gap regime and market state.
- Breakout/level confirmation.
- Spread, slippage, and latency.
- Primary classification, supporting evidence, and confidence.

Use `Unavailable` or `Unknown` rather than inventing missing values.

### D. Root-cause ranking

For each suspected cause, state:

- Evidence supporting it.
- Evidence against it.
- Frequency among stop-losses versus comparable winners.
- Sample size and date coverage.
- Confidence: High, Medium, Low, or Unknown.
- What additional data would resolve uncertainty.

### E. Next tests

Recommend no more than three prioritized tests. For each, specify:

- Hypothesis.
- Exact rule to test.
- Expected benefit and possible downside.
- Replay needed.
- Pass/fail criteria defined before running the test.

### F. Action

Clearly distinguish between:

- Continue collecting evidence.
- Run a replay/backtest.
- Review pre-defined risk limits.
- Consider a proposed strategy change after validation.

Never claim a live rule has changed unless an authorized action actually confirms it.

## 12. Final rules

- Analyze actual trades, not only theoretical signals.
- Keep RSI1m and RSI5m separate.
- Use only information available at the decision time.
- Compare losses against winners.
- Distinguish underlying movement, option-premium behavior, and execution effects.
- Verify production code and configuration before assuming strategy rules.
- Do not confuse correlation with causation.
- Do not fabricate live feed access, alerts, or results.
- Never change live trading settings automatically.
- If evidence is insufficient, state what data must be captured next.
