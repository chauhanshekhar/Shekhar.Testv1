# Current live strategy (read from code and the StrategySettings table on 2026-10-26)

Mode: `TradingMode = Paper`, `AutoTradingEnabled = true` (SystemSettings). Everything below is what Paper is running.
Source of truth: `Trading.Worker/SignalWorker.cs`, `Trading.SignalEngine/Services/SignalEngineService.cs`,
`Trading.SignalEngine/Strategies/*`, `Trading.Worker/Exits/ExitDecisionService.cs`, table `StrategySettings`
(snapshot in `docs/research/2026-10-26-winrate-60-search/strategysettings-now.csv`).

## 1. Signal flow
Snapshot -> Pre-gates -> StrategySelector (by MarketState) -> entry rule (one of 3 paths) -> build option leg/SL/T -> Post-gates -> order -> exit worker.

**Pre-gates (in order):** KillSwitch, EventBlackout, StaleData, OnePosition (one open position across all symbols), AtrGate, MarketRegime.
Commented out in code (not running): MaxDailyTrades, CircuitBreaker, ConsecutiveLoss. `EnableDailyLossLimit = 0`.

**Strategy selector (by MarketState):** Trend/StrongTrend -> Directional (or HeroZero if it has the better historical win rate
in that state/hour); Breakout/Breakdown/Scalping -> Directional; Reversal/Range/NoTrade/News -> no trade; Directional is also
suppressed when MarketRegime is Sideways (except expiry days). Expiry day: before the gamma-blast time Directional, after it HeroZero.

## 2. Entry paths
**A. Directional AND-chain** (all conditions must pass; CALL wins if both pass):
| Condition | CALL | PUT |
|---|---|---|
| Trend filter (on) | 1m EMA20 > EMA50 and LTP > EMA20 | EMA20 < EMA50 and LTP < EMA20 |
| EMA gap (on) | EMA20-EMA50 >= 10 pts (BANKNIFTY 15) | same |
| 5-minute RSI band (closed bars; null fails) | 53 - 70 | 28 - 38 (band 2 40-49 is OFF) |
| Opening RSI band (on, gap >= 0.30%) | gap-up day 45 - 85 | gap-up day 40 - 60 |
| PCR (on) | 0.98 - 1.50 | <= 1.40; SENSEX also >= 0.70 |
| ATR % (MinAtrPercent) | >= 0.03 all symbols | >= 0.03 |
| Candle body | CallBodyPctMinimum inactive | EnableCandleFilter = 0 (off) |
| Delta, day change, OI change, prev-5m high/low, price change | inactive/off | inactive/off |
Not wired: VWAP and volume conditions (VwapSideGate has no rows).

**B. BANKNIFTY PUT BREAKDOWN rule** (on, hardcoded thresholds): BANKNIFTY, trend Bearish, MarketState Breakdown, 5m EMA20 < EMA50,
LTP < EMA20 < EMA50, RSI(1m) <= 35, day change <= -0.20%, ATR% >= 0.035.

**C. Five Gemini opening-range rules (all ON since 2026-10-21, unvalidated; `GeminiIgnoreOnePosition = 1`, so they can run beside an open trade):**
opening range 15 min, window up to 120 min from open. Normal-open ORB CALL (break above OR high, bullish candle, RSI5 >= 55) and PUT
(break below, bearish candle, RSI5 <= 45); gap-up go CALL (break up, PCR >= 1.10, bullish candle); gap-up fade PUT (break down and
(RSI5 >= 75 or bearish candle)); gap-down continuation PUT (break down, bearish candle); gap-down rebound CALL (break up, bullish
candle, RSI5 <= 35). The code comment says earlier research found this family at or below break-even after costs.

## 3. Post-gates (in order)
OnePositionPost (inert while GeminiIgnoreOnePosition=1), OiConfirmation (BANKNIFTY, NIFTY on; SENSEX off), DuplicateSignal (5 min),
Risk, IndexStopFloor, StaleEntry (adverse move > 0.10R blocks), **EarlyTrendGate (on, CALL only: blocks a CALL when the 5m EMA20 is
already above the 5m EMA50)**, SessionTrendGate (off), VwapSide (off), HandoffCooldown (5 min), PutS1 (off), RiskReward, Spread.
NoTradeWindowGate (12:00-12:30 setting on) is commented out in `SignalWorker`, so it does not run.

## 4. Position sizing and exits
- Lots: `AdaptiveLotMin = 4`. Market entry orders, 60 s unfilled-entry timeout.
- Stop: 1.25 x ATR, clamped to 20-100 index points. T1 = 1.5/1.25 = 1.2R, T2 = 1.95/1.25 = 1.56R (ladder replaces T2).
- Target ladder (on): 50% at T1, half of the rest at 3R, the rest at 5R.
- Breakeven arm (on): when premium reaches entry + buffer (BANKNIFTY 15, NIFTY 8, SENSEX 12), stop moves to entry. Sell-half at arm is OFF for all symbols.
- Step-lock (on), ATR trailing after the activation %, protected stop after index T1.
- Exit order: HeroZero safety, EOD force exit, index T1 lock, breakeven arm, protected SL, option SL (broker SL_M), index SL, T2, T1.
- Post-SL: rejected-entry cooldown 5 min.

## 5. Things worth knowing
1. It is Paper, not Live. Three risk gates and the daily loss limit are off by your earlier choice; restore before Live.
2. Real CALLs were taken in Range, Reversal and NoTrade MarketState although the selector suppresses those for the Directional chain.
   Likely the Gemini or breakdown rules, or the snapshot state differing from the state at entry (not verified).
3. The EarlyTrendGate evidence (docs/research/2026-10-08-early-call) is the reason CALLs against a bullish 5m trend are blocked; my
   real-trade check on 2026-10-26 found CALLs entered when the 5m trend was bearish lose (SENSEX: 19 trades, -17.9k), so these two
   facts pull in opposite directions and should be read together.
