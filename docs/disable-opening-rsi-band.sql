/* =====================================================================================================================
   disable-opening-rsi-band.sql
   Switches the opening-conditional RSI band off: StrategySettings.EnableOpeningRsiBand = 0 (global row).
   SignalEngineService.OpeningRsiBand then returns the normal band on every day (CALL 45-70, PUT floor 28) and the
   GapUp/GapDown Call/Put RsiFloor/RsiCeiling rows are ignored. Those rows are NOT deleted, so it can be turned back on.
   Replay 2026-11-24 (current settings, ConsecutiveLossGate off): band on 195 trades / net +35,723; band off 174 / +29,820
   (21 gap-up CALL entries with RSI 70-90 removed; PUT unchanged).
   NOT executed by Claude - review, then run it yourself.
   ===================================================================================================================== */
SET XACT_ABORT ON;
DECLARE @Now datetime2 = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IST, as IstClock.Now

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'EnableOpeningRsiBand' AND Symbol IS NULL)
    UPDATE StrategySettings SET SettingValue = 0, IsActive = 1, UpdatedOn = @Now
     WHERE SettingKey = 'EnableOpeningRsiBand' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableOpeningRsiBand', NULL, 0, 1,
            'Opening-conditional RSI band (GapUp/GapDown Call/Put floor+ceiling rows). 1 = on, 0 = normal band always.', @Now);

COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn FROM StrategySettings WHERE SettingKey = 'EnableOpeningRsiBand';
-- expect SettingValue 0, IsActive 1. Per-symbol rows (Symbol NOT NULL) would override the global row - none expected.
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK (turn it back on; the four GapUp rows were left untouched) --------------------------------------------
UPDATE StrategySettings SET SettingValue = 1, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
 WHERE SettingKey = 'EnableOpeningRsiBand' AND Symbol IS NULL;
------------------------------------------------------------------------------------------------------------------------ */
