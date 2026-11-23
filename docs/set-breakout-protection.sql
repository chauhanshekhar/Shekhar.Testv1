/* =====================================================================================================================
   set-breakout-protection.sql
   Turns BreakoutProtectionService on or off from the database (StrategySettings.EnableBreakoutProtection).
   Live (StrategyManager) and the backtest both read this key: per-symbol row first, then the global row (Symbol IS NULL),
   and with NO row the check is OFF.
   NOT executed by Claude - review, then run it yourself.
   ===================================================================================================================== */
SET XACT_ABORT ON;

DECLARE @Enable  decimal(18, 6) = 0;      -- 1 = on, 0 = off
DECLARE @Symbol  nvarchar(50)   = NULL;   -- NULL = global row; or 'NIFTY' / 'BANKNIFTY' / 'SENSEX'
DECLARE @Now     datetime2      = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IST, as IstClock.Now

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM StrategySettings
           WHERE SettingKey = 'EnableBreakoutProtection'
             AND ((@Symbol IS NULL AND Symbol IS NULL) OR Symbol = @Symbol))
    UPDATE StrategySettings
       SET SettingValue = @Enable, IsActive = 1, UpdatedOn = @Now
     WHERE SettingKey = 'EnableBreakoutProtection'
       AND ((@Symbol IS NULL AND Symbol IS NULL) OR Symbol = @Symbol);
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableBreakoutProtection', @Symbol, @Enable, 1,
            'BreakoutProtectionService: CALL/PUT only after day high/low breakout, pullback and continuation. 1 = on, 0 = off.',
            @Now);

COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WHERE SettingKey = 'EnableBreakoutProtection' ORDER BY Symbol;
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK (switch it back off; no data is deleted) -------------------------------------------------------------
UPDATE StrategySettings SET SettingValue = 0, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
WHERE SettingKey = 'EnableBreakoutProtection';
------------------------------------------------------------------------------------------------------------------------ */
