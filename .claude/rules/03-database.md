# Database and EF Core

- Schema changes go through an EF Core migration in `Trading.Infrastructure/Migrations/` with a
  working `Down()`. Name it descriptively (`AddSignalAtrPctAtEntry`, not `newww`).
- Every data change ships with a rollback script. Never delete production data.
- Prefer a new `StrategySettings` key over a schema change for any tunable or feature flag.
- Queries: target SQL Server 2022 T-SQL. A column alias cannot be referenced in the same
  `SELECT` — compute derived values in `CROSS APPLY (SELECT ...)` or a CTE. Parameterise the
  lookback (`DECLARE @Days int = ...`) instead of hardcoding it.
- Use the real column names from `src/Trading.Domain/Entities/` — verify before writing a query.
  Common traps: `Signals.OptionLtp` is the entry premium (not `Entry`), `MfePrice`/`MaePrice`
  (not `MFE`/`MAE`), `OptionT1` (not `T1`), `StrategySettings.SettingKey/SettingValue` vs.
  `SystemSettings.Key/Value`.
- Classify exits with the `TradingConst.ExitReason` constants plus the literals the exit ladder
  emits (e.g. `PROTECTED_SL_HIT` in `ExitDecisionService`); grep for the current set rather than
  assuming. For win/loss, prefer `TradeJournal.ProfitLoss` sign over exit-reason guessing.
- `IndicatorSnapshotOutcomes`: `NULL` in a window column means *not yet calculated*, never "no
  hit" — filter it out, don't count it as a loss.
