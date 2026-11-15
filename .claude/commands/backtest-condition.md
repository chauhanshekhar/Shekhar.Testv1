---
description: CALL/PUT condition grid - RSI x PCR x Delta with profit, loss and net P&L, then proposed conditions
argument-hint: "<CALL|PUT> [symbol] [explicit conditions]"
---
# Backtest Condition

Sweep and rank entry conditions for `$ARGUMENTS`, following
`.claude/skills/backtest-condition/SKILL.md`.

Examples:

```text
/backtest-condition PUT
/backtest-condition CALL
/backtest-condition nifty put
/backtest-condition CALL RSI 50-60
```

Mandatory behavior:
1. Direction (CALL/PUT) is required; a symbol is optional. No symbol = global view plus a
   per-symbol breakdown.
2. Apply every Measurement Guard from `.claude/skills/propose-condition/SKILL.md`: one row per
   symbol-minute, distinct days beside trades, net P&L (never gross), break-even win rate beside
   actual, both buffer columns.
3. Run `.claude/skills/backtest-condition/grid_backtest.py`. Every table shows Trades, Wins,
   Losses, Win %, Profit ₹, Loss ₹, Net ₹ (+/-), PF, Days, Trades/day and Gross profit/day ₹,
   for CALL and PUT separately; then 2-4 ranked candidate conditions.
4. Map to an existing `StrategySettings` key (script with ROLLBACK/VERIFY) or hand off to
   `/new-condition` — never INSERT a key no code reads.
5. Read-only. Output to `docs/research/<yyyy-mm-dd>-backtest-condition-<direction>[-<symbol>]/`.
