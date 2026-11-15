# Project Map

CRA Trading Platform — a **live options trading system** (NIFTY / BANKNIFTY / SENSEX) on .NET 10,
EF Core, SQL Server 2022, Angular, Docker on Ubuntu EC2, broker = Groww API. Single git repo;
`TradingPlatform.slnx` at the root.

## Projects (`src/`)

| Project | Owns |
|---|---|
| `Trading.Domain` | Entities (`Entities/`), constants (`TradingConst.cs` — `ExitReason`, `TradeStatus`, ...), `IstClock` |
| `Trading.Application` | Interfaces (`Interfaces/`), models, application services |
| `Trading.Infrastructure` | `TradingDbContext`, EF migrations, repositories, Groww broker client (`Brokers/Groww/`), `StrategySettingsService`, `TradingSettings` |
| `Trading.SignalEngine` | `SignalEngineService`, entry evaluators (`CallEntryEvaluator`, ...), `AtrRiskService`, scoring/ranking, strategies |
| `Trading.Worker` | `SignalWorker` (gate pipeline), `ExitWorker` + `Exits/ExitDecisionService`, `BrokerOrderSyncWorker`, `BrokerPositionSyncWorker`, `SignalGates/` |
| `Trading.API` | Controllers, SignalR hubs, health checks |
| `Trading.Tests` | xUnit tests (`Fakes/`, `Support/`) — one file per behaviour, e.g. `BreakevenArmTests.cs` |
| `Trading.Web` | Angular UI — referenced by `.claude/launch.json` (`trading-ui`, port 4200) but **not present in this checkout**; don't assume its code exists |

## Where configuration lives — do not mix these up

- **`StrategySettings` table** (`StrategySetting`: `SettingKey`, `Symbol` nullable, `SettingValue`
  decimal, `IsActive`, `Description`, `UpdatedOn`) — every strategy/risk threshold and every
  `Enable<X>` feature flag. Read only through `StrategySettingsService`
  (`GetBool/GetDecimal/GetInt/IsSettingActive(key, symbol, default)`), cached and refreshed every
  ~5 minutes. Lookup order: exact `Symbol` → `Symbol IS NULL` (global) → compiled default.
- **`SystemSettings` table** (`Key`, `Value`, `UpdatedOn`) — runtime operational state only
  (`AutoTradingEnabled`, `TradingMode`, Groww token/credentials, worker checkpoints). Never put
  strategy toggles here. Contains **credentials** — never `SELECT *` it or paste its rows.
- **`TradingSettings` / `appsettings.json`** — compiled defaults (e.g. `AtrTarget1Multiplier`).

## Signal gates

`Trading.Worker/SignalGates/ISignalGate.cs` defines `IPreSignalGate` (market-level, runs before a
strategy is chosen) and `IPostSignalGate` (sees the built `Signal`). Gates return `null` to
proceed or an abort-reason string, and are registered in the static `PreGates` / `PostGates`
arrays in `Trading.Worker/SignalWorker.cs` — order matters.

## Analysis output

SQL scripts and research write-ups go in `docs/` (existing convention: `docs/<verb>-<topic>.sql`,
e.g. `set-banknifty-rule-kill-switch.sql`, `diagnose-no-signals-today.sql`). Command-generated
research reports go in `docs/research/<yyyy-mm-dd>-<command>-<arg>/`. Never leave ad-hoc `.sql`
files at the repo root.
