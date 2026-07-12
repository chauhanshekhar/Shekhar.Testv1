# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CRAMAN is an algorithmic trading platform for Indian equity and derivatives markets (NSE). It supports Angel One and Groww brokers with live trading, paper trading, and backtesting.

## Build & Run

**Build everything:**
```bash
dotnet build src/TradingPlatform.slnx -c Release
```

**Run API (dev):**
```bash
cd src/Trading.API && dotnet run
# Swagger at http://localhost:5000/swagger
```

**Run Angular frontend:**
```bash
cd src/Trading.Web
npm install
npm start         # http://localhost:4200
npm run generate:api  # regenerate API client from Swagger (nswag)
```

**Run background worker:**
```bash
cd src/Trading.Worker && dotnet run
```

**Run via Docker (production):**
```cmd
.\deploy.cmd
# API: :8080, UI: :4200, SQL Server: :1433
```

**Run tests:**
```bash
dotnet test tests/Trading.Tests/Trading.Tests.csproj
```

**Run a single test:**
```bash
dotnet test tests/Trading.Tests/ --filter "FullyQualifiedName~TestMethodName"
```

**EF Core migrations:**
```bash
cd src/Trading.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../Trading.API
dotnet ef database update --startup-project ../Trading.API
```

## Architecture

Clean layered architecture — dependency flow is always inward:

```
Trading.API / Trading.Worker
      ↓
Trading.Application   (interfaces + DTOs)
      ↓
Trading.Infrastructure (EF Core, broker clients, repositories)
      ↓
Trading.Domain        (entities, enums, value objects)
      ↓
Trading.SignalEngine   (indicator math, signal generation, risk)
```

### Layer responsibilities

**Trading.Domain** — entity models (`Signal`, `Order`, `Position`, `Portfolio`, `Candle`, `OptionChainData`), enums (`SignalStatus`, `TradingMode`), no external dependencies.

**Trading.Application** — service interfaces (`IOrderService`, `IMarginService`, `IMarketDataFeed`, `IHistoricalDataService`) and request/response DTOs for both brokers. No implementation here.

**Trading.Infrastructure** — `TradingDbContext` (EF Core, SQL Server), Repository + Unit of Work pattern, broker HTTP clients (Angel One, Groww), Telegram/WhatsApp alert services, instrument cache. Migrations live in `Trading.Infrastructure/Migrations/`.

**Trading.SignalEngine** — technical indicators (EMA, RSI, ATR, VWAP, PCR, OI analysis), `SignalEngineService`, `SignalBuilder`, `RiskManagementService`, `PaperTradingService`, `BacktestService`.

**Trading.API** — 20 ASP.NET Core controllers, SignalR hubs for real-time market data and signal updates, JWT auth middleware, `/health` endpoint.

**Trading.Worker** — .NET Worker Service hosting background jobs: signal generation loop, order exit management, position updates, daily PnL calculations.

**Trading.Web** — Angular 21 SPA with SSR. API client auto-generated via nswag from the Swagger spec. Real-time updates via SignalR client. Bootstrap 5 + Chart.js for UI.

## Key Patterns

- **Repository + Unit of Work**: all DB access goes through repositories, committed via `IUnitOfWork`
- **Dependency injection setup**: service registrations are in extension classes (e.g., `AddDatabase`, `AddBrokers`, `AddLogging`) called from `Program.cs`
- **Multi-broker plugin model**: Angel One and Groww each implement the same `Application` interfaces; swap by config
- **Circuit breaker**: `SystemSettings` table stores `AutoTradingEnabled`; worker checks max-loss/day and max-trades/day limits before placing orders
- **Paper trading**: fully mirrors live flow through `PaperTradingService` with no real orders

## Configuration

| Scope | File | Notes |
|---|---|---|
| Dev | `appsettings.json` | Broker keys, JWT, DB connection string |
| Local override | `appsettings.Local.json` | Gitignored |
| Worker secrets | `secrets.json` | User Secrets |
| Docker | `.env` | Gitignored; copy from `.env.example` |

Dev database: `Server=(LocalDb)\MSSQLLocalDB;Database=TradingDB_Dev`

DB migrations run automatically on startup when `AutoMigrate: true` is set in config.

## Database Key Tables

`Signals`, `Orders`, `Positions`, `TradeJournal`, `Portfolio`, `PaperTrades`, `Candles`, `MarketTicks`, `OptionChainData`, `TradingInstruments`, `ExchangeHoliday`, `Users`, `SystemSettings`, `AppError`

## Frontend Notes

- Angular runs strict TypeScript (`tsconfig.json`)
- After changing any API endpoint signature, re-run `npm run generate:api` to sync the generated client
- SSR is enabled; server-side rendering builds to `dist/trading.web/server/`
- Tests use Vitest (`npm test`)
