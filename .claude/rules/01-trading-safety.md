# Trading Safety

Capital protection overrides code quality and overrides speed. (Source: `AGENTS.md` Safety and
AI Decision policies — that file remains the full role catalogue.)

- Never remove or weaken: risk checks, duplicate-order protection, `TradeJournal` writes,
  position synchronization, broker verification, stop loss, protected stop loss, trailing stop.
- Never create a duplicate SELL order. `BrokerOrderSyncWorker` is the source of truth for order
  state.
- Preserve the signal lifecycle: Pending → Active → ExitPending → Closed.
- AI may analyse, backtest, recommend, compare, optimise, rank. AI must **not**, without explicit
  user approval in chat: place trades, modify production, change database values, deploy, push,
  or merge.
- Never guess. Every strategy/parameter recommendation needs historical evidence: sample size,
  baseline vs. candidate, and the query that produced it.
- Never optimise for trade count. Optimise for profit factor, expectancy, and drawdown.
- Never use future information in a backtest (no look-ahead: a value is usable only if it was
  known at the snapshot/entry time).
- Never expose credentials: no secrets in code, scripts, docs, or command output. `SystemSettings`
  holds Groww credentials — select specific keys only.
