---
name: backtest-ablation
description: Full backtest of the trading system exactly as configured live, then switch off each enabled condition/gate one at a time and show what each one adds or costs. Only input is an optional start date (YYYY-MM-DD); with no date it runs the whole stored history. Use when the user says backtest, full backtest, run backtest, ablation, "which condition helps", "which gate is useless", or "test all conditions".
---

# Backtest Ablation (live baseline + one-by-one disable)

Answer two questions:
1. How does the system perform with today's LIVE settings?
2. For every condition/gate that is ON, what happens if only that one is switched OFF?

Read-only research. Follow .claude/rules/01-trading-safety.md and 02-approval-gate.md.
Never change StrategySettings, appsettings or production data. Never commit, push or deploy.

---

## 0. Input and defaults

The ONLY input is an optional start date.

| Input | Meaning |
|---|---|
| 2026-11-01 (any YYYY-MM-DD) | replay from that date to the latest stored data: add --from 2026-11-01 to EVERY run |
| nothing | full backtest: whole stored history, NO --from on any run |

Rules:
- Accept YYYY-MM-DD only. Anything else (symbols, sides, settings) is ignored - say so in one line and continue.
- An invalid or future date: stop and say the date is invalid. Do not guess.
- Call the chosen value <FROM>: either --from <date> or empty.

Everything else is fixed, never asked:

| Item | Value |
|---|---|
| Settings | exactly as live: StrategySettings + SystemSettings.TradingMode (no --trading-mode) |
| Side / symbols | BOTH, all symbols |
| Costs | --cost-per-order 20 --slippage-points 1 |
| Regime | --regime-replay (production routing) |
| Output root | docs/research/<today yyyy-MM-dd>-ablation-<from date or "full">/ |

Only stop to ask if the build or the database connection fails.

## 1. Build


dotnet build src/Trading.Backtest
dotnet build src/Trading.Tests

Stop and report if either fails.

## 2. Collect the live switches

Read-only query (sqlcmd, or the same connection the backtest uses):

sql
SELECT SettingKey, ISNULL(Symbol,'ALL') AS Symbol, SettingValue
FROM StrategySettings
WHERE IsActive = 1 AND SettingKey LIKE 'Enable%' AND SettingValue <> 0
ORDER BY SettingKey, Symbol;


Each row is one candidate to disable. A per-symbol row is disabled with KEY=0@SYMBOL, a global row with KEY=0.

Also include switches that are ON by compiled default with no row (GetBool(key, symbol, true) in code), e.g.
EnableBreakevenArm, EnableIndexStopFloor. Find them with:
grep -rn 'GetBool("Enable[A-Za-z0-9]*", [^,]*, true)' src
Skip any of those that have a row with value 0 (already off).

## 3. Classify: can the replay see this switch?

For each key:
grep -rln "\"<KEY>\"" src/Trading.Backtest src/Trading.SignalEngine

- Found -> REPLAYED (the replay, or the live AND-chain it calls, reads it). Test it.
- Not found -> NOT REPLAYED. Do not run it; list it in the report as "not testable in replay".
  Known examples (2026-11-25): EfficiencyRatioGate, StopNoiseGate, Trend5mGate, OpeningWarmupGate,
  CloseBreakoutGate, MaxDailyTradesGate, EnableAtr5mStop, Target1RMultiple / Target2RMultiple
  (Simulation/ExitModel.cs still uses the 1m-ATR stop and default T1/T2).

Never report a "no effect" result for a switch the replay does not read.

## 4. Parity (trust level) - run once


dotnet run --project src/Trading.Backtest -- --signal-parity --regime-replay <FROM> --out <root>/parity

With no date, --signal-parity uses its own default start (--parity-from default in Program.cs) - state the start
date it used in the report.

Read signal-parity-summary.md -> "reproduced by the replay: X of Y".

| Reproduced | Trust label on every result |
|---|---|
| >= 90% | USABLE |
| 70-90% | DIRECTIONAL ONLY |
| < 70% | NOT RELIABLE - show results but say decisions must wait for parity fixes |

## 5. Baseline (live)


dotnet run --project src/Trading.Backtest -- --backtest-trades --regime-replay <FROM> ^
  --cost-per-order 20 --slippage-points 1 --dump-trades --out <root>/baseline


## 6. One-by-one disable

For EVERY replayed switch, run the identical command with only that switch off:


dotnet run --project src/Trading.Backtest -- --backtest-trades --regime-replay <FROM> ^
  --cost-per-order 20 --slippage-points 1 --dump-trades ^
  --set "<KEY>=0[@SYMBOL]" --out <root>/off-<KEY>[-<SYMBOL>]


Rules:
- Exactly ONE switch changed per run. Same <FROM> and flags as the baseline.
- If a run fails, record the error and continue with the next switch.
- If a run's trades are identical to the baseline, mark it "no trades affected" (it IS replayed, it just never fired
  in this period) - never call it useless.

## 7. Metrics (compute, never estimate)

From each run's trades file - overall, per side (CALL/PUT) and per symbol:
trades, win %, gross profit, gross loss, net P&L after costs, profit factor, expectancy per trade,
max drawdown, average win, average loss.

Write <root>/compare.py (or reuse helpers in .claude/skills/backtest-condition/) that reads every trades.csv
and writes <root>/ablation.csv. All arithmetic is done by the script, never in prose.

## 8. Verdict per switch

Compare "switch OFF" against the baseline:

| If turning it OFF... | Verdict |
|---|---|
| lowers net P&L or profit factor, or raises drawdown | HELPS - keep |
| raises net P&L AND profit factor without a bigger drawdown | HURTS - candidate to remove |
| changes fewer than 5 trades | TOO FEW TRADES - no decision |
| mixed (P&L up but drawdown up, etc.) | MIXED - explain |
| not read by the replay | NOT TESTABLE |

If a switch hurts overall but helps one side or symbol, say so - it may need a per-symbol row, not removal.

## 9. Report

Write <root>/README.md and give the user this summary:

1. Period (from <date> or full history: <first date> to <last date>), trades, trust label (parity X of Y).
2. *Live baseline*: trades, win %, net P&L, profit factor, expectancy, max drawdown - overall, CALL, PUT, per symbol.
3. *Ablation table*, sorted by impact on net P&L:

| Switch | Symbol | Trades Δ | Win % Δ | Net P&L Δ | PF Δ | Max DD Δ | Verdict |
|---|---|---|---|---|---|---|---|

4. *Not testable* list (section 3) with the replay file that would need changing.
5. *Top 3 findings* in plain words, each with its evidence row.
6. *Recommendation*: keep / remove / per-symbol / paper-test - never "deploy". Any setting change goes through the
   approval gate with this report as the evidence.

## Never

- Ask for any input other than the optional date.
- Change more than one switch per run.
- Use real Signals/TradeJournal P&L to judge a switch (simulated outcomes only; real data is only for parity).
- Present a NOT TESTABLE switch as having no effect.
- Change settings, commit, push or deploy.