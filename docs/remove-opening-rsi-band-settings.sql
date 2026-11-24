/* =====================================================================================================================
   remove-opening-rsi-band-settings.sql
   Deletes the StrategySettings rows of the removed opening-conditional RSI band (SignalEngineService.OpeningRsiBand was
   deleted from the code): EnableOpeningRsiBand and every GapUp/GapDown Call/Put RsiFloor/RsiCeiling row.
   KEEP OpeningRsiGapPct - CallPcr filter and FastEmaGate still read it.
   Run disable-opening-rsi-band.sql first or together; either order is safe once the new Worker build is deployed.
   NOT executed by Claude - review, then run it yourself.
   ===================================================================================================================== */
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DELETE FROM StrategySettings
 WHERE SettingKey = 'EnableOpeningRsiBand'
    OR SettingKey LIKE 'GapUp%Rsi%' OR SettingKey LIKE 'GapDown%Rsi%';
PRINT CONCAT('Rows deleted: ', @@ROWCOUNT, ' (expected 5)');
COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT COUNT(*) AS RowsLeft FROM StrategySettings
 WHERE SettingKey = 'EnableOpeningRsiBand' OR SettingKey LIKE 'GapUp%Rsi%' OR SettingKey LIKE 'GapDown%Rsi%';   -- expect 0
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK (re-create the rows; the removed code would also have to be restored for them to do anything) ----------
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableOpeningRsiBand', NULL, 1.000000, 1, NULL, DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GapUpCallRsiCeiling', NULL, 90.000000, 1, NULL, DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GapUpCallRsiFloor', NULL, 45.000000, 1, NULL, DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GapUpPutRsiCeiling', NULL, 60.000000, 1, NULL, DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GapUpPutRsiFloor', NULL, 40.000000, 1, NULL, DATEADD(MINUTE, 330, SYSUTCDATETIME()));
------------------------------------------------------------------------------------------------------------------------ */
