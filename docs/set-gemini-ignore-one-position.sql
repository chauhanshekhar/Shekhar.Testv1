/* ============================================================================
   GeminiIgnoreOnePosition — one open position PER STRATEGY                2026-10-21
   ----------------------------------------------------------------------------
   Ships OFF. Claude does not execute this file (.claude/rules/02-approval-gate.md).

   WHAT IT DOES (code: SignalGates/OnePositionGate.cs + OnePositionPostGate.cs)
   -------------------------------------------------------------------------
   Flag = 0 (default / no row): the original rule. ONE open position across ALL
   symbols and ALL strategies.

   Flag = 1: one open position PER STRATEGY.
     - Strategies: Gemini (Signals.SelectedStrategy = 'GeminiOpening') and
       everything else (chain, HeroZero, approved rules) share one slot each.
     - A signal is blocked while a position of its OWN strategy is open.
     - A position of the other strategy does NOT block it.
     - Neither may enter the same symbol AND direction as any open position.
     - The live-Orders exposure check (entry in flight / bought-not-sold after
       the status went terminal) still applies to signals that are not open.
   Up to TWO positions can therefore be open at once.

   *** BEFORE ENABLING ***
   - Capital and margin exposure can roughly double. Use PAPER first.
   - BrokerPositionSyncWorker with two simultaneous positions has not been
     verified; do not use on Live until it has.
   - With the flag on, the strategy pipeline runs while a position is open, so
     StrategyExecutionLog gets a row for each chain signal later blocked by
     OnePositionPostGate (more log rows, no extra orders).

   SAFETY
   ------
   - Idempotent upsert; re-running changes nothing.
   - Touches only the GeminiIgnoreOnePosition row.
   - Per symbol (@Symbol) or global (NULL). Settings cache ~5 minutes.
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @Symbol nvarchar(50)  = NULL;   -- NULL = global, or 'BANKNIFTY' etc.
DECLARE @Enable decimal(18,6) = 0;      -- <<< 1 = one open position per strategy

MERGE dbo.StrategySettings AS t
USING (SELECT N'GeminiIgnoreOnePosition' AS SettingKey, @Enable AS SettingValue,
              N'1 = one open position per strategy (Gemini vs rest) instead of one overall. See docs/set-gemini-ignore-one-position.sql' AS Descr) AS s
   ON t.SettingKey = s.SettingKey
  AND ISNULL(t.Symbol, N'~') = ISNULL(@Symbol, N'~')
WHEN MATCHED THEN
    UPDATE SET SettingValue = s.SettingValue, IsActive = 1, UpdatedOn = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (s.SettingKey, @Symbol, s.SettingValue, 1, s.Descr, SYSDATETIME());

PRINT CONCAT('GeminiIgnoreOnePosition rows upserted: ', @@ROWCOUNT);
GO

/* VERIFY */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey = N'GeminiIgnoreOnePosition';

/* ---------------------------------------------------------------------------
   KILL SWITCH — back to one position overall.
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = N'GeminiIgnoreOnePosition';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   FULL ROLLBACK — remove the row.
   ---------------------------------------------------------------------------
DELETE FROM dbo.StrategySettings WHERE SettingKey = N'GeminiIgnoreOnePosition';
   --------------------------------------------------------------------------- */
