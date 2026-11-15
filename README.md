# CRA Trading Platform

A **live options trading system** for NIFTY, BANKNIFTY and SENSEX. It builds candles and
indicators from market data, generates signals through a gated signal engine, places and manages
orders with the Groww broker API, and records every trade for later analysis.

> Capital protection comes before everything else. Read [`CLAUDE.md`](CLAUDE.md) and
> [`AGENTS.md`](AGENTS.md) before changing signal, exit, order or risk code.

## Stack

- .NET 10 (ASP.NET Core API + worker service), Entity Framework Core
- SQL Server 2022
- Docker, deployed to Ubuntu on EC2
- SignalR (live updates), Telegram / WhatsApp notifications
- Broker: Groww API

## Solution layout

`TradingPlatform.slnx` at the root; all projects are under `src/`.

| Project | Purpose |
|---|---|
| `Trading.Domain` | Entities and constants (`TradingConst` — exit reasons, statuses) |
| `Trading.Application` | Interfaces, models, application services |
| `Trading.Infrastructure` | `TradingDbContext`, EF Core migrations, repositories, Groww client, `StrategySettingsService` |
| `Trading.SignalEngine` | Signal generation — entry evaluators, ATR risk, scoring and ranking, strategies |
| `Trading.Worker` | Background workers — market feed, candles/EMA, option chain, `SignalWorker` (signal gates), `ExitWorker`, broker order/position sync, Groww token refresh, daily PnL, notifications |
| `Trading.API` | REST controllers, SignalR hubs, health checks |
| `Trading.Tests` | xUnit tests |

The Angular UI (`Trading.Web`) is **not in this repository**. `.claude/launch.json` still
references `src/Trading.Web`, so that preview won't start from this checkout.

## Getting started

### Prerequisites

- .NET 10 SDK
- Docker (for SQL Server and container builds)

### Configuration

Real configuration files are gitignored — copy the examples and fill in your own values:

| Copy from | To | Holds |
|---|---|---|
| `.env.example` | `.env` | SQL Server SA password, Groww API key and TOTP secret (used by `docker-compose.yml`) |
| `src/Trading.API/appsettings.json.example` | `appsettings.json` (same folder) | API settings, connection string |
| `src/Trading.Worker/appsettings.json.example` | `appsettings.json` (same folder) | Worker settings, trading defaults |
| `*/appsettings.Production.json.example` | `appsettings.Production.json` | Production overrides |
| `deploy.cmd.example` | `deploy.cmd` | Deployment host and key (env-var driven) |

Never commit `.env`, `appsettings.json`, `appsettings.Production.json` or `deploy.cmd`. If a
Groww credential is ever exposed, revoke it on the Groww Cloud API Keys page and generate a new
one.

### Build and test

```bash
dotnet build TradingPlatform.slnx
dotnet test src/Trading.Tests
```

### Run with Docker Compose

```bash
docker compose up -d --build
```

This starts `sqlserver` (port 1433), `trading-api` (port 8080) and `trading-worker`. The API and
worker wait for SQL Server's health check before starting.

### Deploy

`deploy.cmd` (from `deploy.cmd.example`) builds both images, streams them to the server over SSH,
swaps the containers only after both images load, and checks that each container is running.
Deployment is always manual — never automated.

## Runtime configuration

Three places, with different purposes:

| Where | What goes there | How it's read |
|---|---|---|
| `StrategySettings` table | Strategy and risk thresholds, and every `Enable<X>` on/off flag, optionally per symbol | `StrategySettingsService` — cached, refreshed about every 5 minutes; lookup is symbol row → global row → code default |
| `SystemSettings` table | Runtime state only: `AutoTradingEnabled`, `TradingMode`, Groww token, worker checkpoints | Directly by the owning service. Contains credentials — never `SELECT *` it |
| `appsettings.json` / `TradingSettings` | Compiled defaults, e.g. `AtrTarget1Multiplier` | Options binding at startup |

Changing a `StrategySettings` value takes effect without a redeploy. Scripts that do this live in
`docs/` (for example `docs/set-banknifty-rule-kill-switch.sql`) and always include a rollback
block.

## Docs and analysis

- `docs/*.sql` — diagnostic, validation and settings scripts (`diagnose-*`, `validate-*`,
  `set-*`).
- `docs/research/<yyyy-mm-dd>-<topic>/` — generated research reports.

## Day types — always split research by how the session opened AND what it did next

Results that look flat in aggregate are often two regimes cancelling out, so **every entry-rule
result must be reported per day type, never only as a total.** The opening is measured from the
index open against the previous session's close (the +/-0.30% convention used by
`--opening-types` in `Trading.Backtest`); what the session did next is the second axis.

| # | Opening | What the session did next | Shorthand |
|---|---|---|---|
| 1 | Gap down | kept falling | gap-down continuation |
| 2 | Gap down | stayed in a range | gap-down range |
| 3 | Gap down | reversed and rose | gap-down reversal |
| 4 | Gap up | kept rising | gap-up continuation |
| 5 | Gap up | reversed and fell | gap-up reversal |
| 6 | Gap up | stayed in a range | gap-up range |
| 7 | Normal (< +/-0.30%) | trended either way, or ranged | normal |

A PUT wants 1 and 5; a CALL wants 3 and 4. Types 2, 6 and the range half of 7 are where entry
filters earn their keep, because both sides get chopped.

**What is measured today, and what is not.** The opening axis is implemented: `OpeningType`
(GapUp / GapDown / Normal) in `Trading.Backtest/Entry/EntryEvaluator.cs`, swept by
`--opening-types` and `--stretch-by-opening`, and derived for real trades from
`IndicatorSnapshots.DayOpen` / `YesterdayClose` (exported by
`.claude/skills/backtest-condition/real-signals.sql`). **The second axis — continuation vs
reversal vs range — is NOT implemented.** Nothing in the codebase classifies what a session did
after the open, so rows 1-6 above cannot currently be separated. Do not report them as if they
were; build the classifier first.

**Why this is here** (2026-10-15, `docs/research/2026-10-15-stretch-by-opening/`): a proposed cap
on entry distance from EMA20 looked like a clear BANKNIFTY PUT improvement in aggregate
(-4,038 -> +26,378 after costs). Split by opening it was doing three different things — rescuing
GapUp (-18,323 -> +4,790) and Normal (-430 -> +14,817) while **damaging GapDown**
(+14,714 -> +6,771), the one opening already profitable without it. The aggregate hid a rule that
was wrong for a third of sessions. See `docs/propose-banknifty-put-stretch-cap.md`.

**Costs are not optional.** Pass `--cost-per-order 20 --slippage-points 1` on every run. Gross
figures are misleading here: the production PUT chain is +20,805 gross and **-30,455 after costs**.

## Claude Code setup

The `.claude/` folder configures Claude Code for this repository:

- `rules/` — always-on rules: project map, trading safety, approval gate, database, existing
  code first.
- `skills/` — specialist playbooks (signal engine, exit engine, risk, backtesting, parameter
  optimization, market regime, and more). `AGENTS.md` maps each role to its skill.
- `commands/` — slash commands; each one just points at the matching `skills/<name>/SKILL.md`,
  which holds the real procedure:

| Command | Arguments | Does |
|---|---|---|
| `/analyze-signals` | `[days=7] [symbol]` | Read-only analysis of recent signals; finds leaks and proposes two fixes |
| `/backtest-be` | `[days=7] [symbol] [mult=..] [fixed=..] [buffer=..]` | Backtests the breakeven-arm exit rule using the live formula |
| `/backtest-condition` | `<CALL\|PUT> [symbol] [explicit conditions]` | RSI x PCR x Delta grid with profit, loss and net P&L, then proposed condition sets |
| `/propose-condition` | `<symbol> [days] [CALL\|PUT] [window=5\|10\|15]` | Researches an evidence-backed entry condition |
| `/new-condition` | `<GateName> <condition description>` | Implements a new signal gate with an off-by-default flag, tests and a rollback script |

Analysis commands are read-only. Reports are written to `docs/research/<yyyy-mm-dd>-<command>-<arg>/`.

### If a skill gives wrong or stale output

Skills read the database and the code, so check these first:

- **Wrong results** — the research data has known traps (broken `IndicatorSnapshotOutcomes` join,
  snapshot `Ltp` is a candle close, `Rsi5m` vs the 1-minute `RsiAtEntry`). Check the notes in
  your Claude memory folder (`MEMORY.md`) before trusting an analysis.
- **Wrong column or setting names** — use the real names from `src/Trading.Domain/Entities/`;
  `StrategySettings` uses `SettingKey`/`SettingValue`, `SystemSettings` uses `Key`/`Value`.
- **Skill not triggering or answering the wrong thing** — the `description` in each
  `SKILL.md` front matter decides when it loads, and it must match that skill's content.
- **Old behaviour described** — if code and a skill disagree, the code wins; fix the skill.


Claude never runs database writes, deploys, commits or pushes. Code changes are proposed first
and made only after explicit approval.
