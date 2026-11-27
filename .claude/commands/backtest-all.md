---
description: Live-settings backtest, then disable every enabled condition one by one and rank their impact
argument-hint: "[from-date YYYY-MM-DD]  (empty = full history)"
---
Run .claude/skills/backtest-ablation/SKILL.md end to end.

Input: $ARGUMENTS
- A date (YYYY-MM-DD) -> replay from that date.
- Empty -> full backtest on the whole stored history.
Ask nothing else.