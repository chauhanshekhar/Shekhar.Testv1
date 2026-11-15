# Candidate 1 - Gap-Down PUT, RSI5m 45-55, AtrPct<0.03, 1m+5m EMA20<EMA50 (read-only, 2026-10-28)

Gap-down = (DayOpen - YesterdayClose)/YesterdayClose <= -0.30% (Trading.Backtest default).
Source: IndicatorSnapshots (from 2026-07-10) + 1m Candles forward window (snapshot-forward.sql), and
IndicatorSnapshotOutcomes join (c1-outcomes-join.sql). Scripts: investigate_c1.py.

- 84.74% was NOT reproduced by any slice (5/10/15 min, per-second, per-minute, post-09-30, resolved-only).
- Per-second snapshot rows: 6,489 rows = only 158 distinct symbol-minutes (41x inflation) on 6 symbol-days.
  Three days (NIFTY/SENSEX 09-24, 09-28) are 85% of rows; overlapping minutes are not independent.
- Outcome join is valid for these rows (EntryPrice = Ltp, same symbol); outcome TARGET agrees with raw candles.
- Minute-dedupe 15m, +/-20pt: target 51% of all, 69% of resolved (NONE 25% is excluded from "resolved").
- Candle-only, 20pt/20pt, 15m: target-first 51-66% (both-hit minutes order unknown), 30m 50-80%.
- Non-overlapping entries: n=27. Days differ widely: NIFTY 09-28 8% target, 10-12 SENSEX 100% (5 minutes).
- Rows >= 2026-09-30 give 98.6-100% target, but are 358 per-second rows from 2 days (10-07, 10-12).
- 20 pts = 3.4 ATR for NIFTY (ATR ~5.9) but 1.1 ATR for SENSEX (ATR ~17.5): barrier is not comparable across symbols.
- Costs not applied; no option premium path.
