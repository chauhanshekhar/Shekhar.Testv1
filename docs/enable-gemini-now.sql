/* ============================================================================
   ENABLE Gemini rules — 4 rules, all symbols, one open position per strategy  2026-10-21
   ----------------------------------------------------------------------------
   Claude does not execute this file (.claude/rules/02-approval-gate.md). Run it yourself.

   TURNS ON (global):
     EnableGeminiGapDownContinuation   gap-down: below OR low + bearish candle      -> PUT
     EnableGeminiGapUpFade             gap-up:   below OR low + (RSI>=75 OR bearish) -> PUT
     EnableGeminiGapUpGo               gap-up:   above OR high + PCR>=1.1 + bullish  -> CALL
     EnableGeminiNormalOrb             no gap:   OR break + RSI band + candle        -> CALL/PUT
     GeminiIgnoreOnePosition           one open position per strategy (up to 2 open)
   LEFT OFF: EnableGeminiGapDownRebound.

   *** BEFORE RUNNING ***
   - The new worker build must be deployed first, or the flags do nothing.
   - These rules are UNVALIDATED; stored research puts this family at or below
     break-even after costs. Run on PAPER.
   - BrokerPositionSyncWorker with two simultaneous positions is not verified.
   - The guard below refuses to run unless TradingMode looks like Paper. If your
     TradingMode value is stored differently, read the SELECT output and set
     @RequirePaper = 0 deliberately.

   SAFETY: idempotent upsert; touches only the five rows above; kill switch and
   rollback at the bottom. Settings cache refreshes in ~5 minutes, no restart.
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @RequirePaper bit = 1;

-- Specific keys only: SystemSettings holds credentials, never SELECT *.
SELECT [Key], [Value] FROM dbo.SystemSettings WITH (NOLOCK)
WHERE [Key] IN (N'TradingMode', N'AutoTradingEnabled');

IF @RequirePaper = 1
   AND NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WITH (NOLOCK)
                   WHERE [Key] = N'TradingMode' AND [Value] LIKE N'%Paper%')
    THROW 50001, 'TradingMode is not Paper - aborting. Review the SELECT above; set @RequirePaper = 0 only if this is intended.', 1;

DECLARE @Symbol nvarchar(50) = NULL;   -- NULL = global

DECLARE @rows TABLE (SettingKey nvarchar(100), SettingValue decimal(18,6), Descr nvarchar(400));
INSERT INTO @rows VALUES
 (N'EnableGeminiGapDownContinuation', 1, N'Gemini gap-down continuation PUT on/off'),
 (N'EnableGeminiGapUpFade',           1, N'Gemini gap-up fade PUT on/off'),
 (N'EnableGeminiGapUpGo',             1, N'Gemini gap-up go CALL on/off'),
 (N'EnableGeminiNormalOrb',           1, N'Gemini normal-open ORB rule on/off. See docs/enable-gemini-now.sql'),
 (N'GeminiIgnoreOnePosition',         1, N'1 = one open position per strategy (Gemini vs rest). See docs/set-gemini-ignore-one-position.sql');

MERGE dbo.StrategySettings AS t
USING @rows AS s
   ON t.SettingKey = s.SettingKey
  AND ISNULL(t.Symbol, N'~') = ISNULL(@Symbol, N'~')
WHEN MATCHED THEN
    UPDATE SET SettingValue = s.SettingValue, IsActive = 1, UpdatedOn = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (s.SettingKey, @Symbol, s.SettingValue, 1, s.Descr, SYSDATETIME());

PRINT CONCAT('Gemini enable rows upserted: ', @@ROWCOUNT);
GO

/* VERIFY */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive,
       CASE WHEN IsActive = 1 AND SettingValue <> 0 THEN 'ON' ELSE 'off' END AS State, UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey LIKE N'EnableGemini%' OR SettingKey = N'GeminiIgnoreOnePosition'
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   KILL SWITCH — turn everything back off (rows stay, values 0).
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey LIKE N'EnableGemini%' OR SettingKey = N'GeminiIgnoreOnePosition';
   --------------------------------------------------------------------------- */
