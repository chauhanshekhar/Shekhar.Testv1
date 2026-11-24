/* =====================================================================================================================
   remove-put-rsi-settings.sql
   Deletes the StrategySettings rows of the removed PUT RSI condition (SignalEngineService / PutEntryEvaluator no longer read
   them): PutRsiFloor, PutRsiCeiling, EnablePutRsiBand2, PutRsiBand2Floor, PutRsiBand2Ceiling.
   Live behaviour does not change: PutRsiCeiling was already inactive, so the PUT RSI condition was already skipped.
   NOT executed by Claude - review, then run it yourself, after the new Worker build is deployed.
   ===================================================================================================================== */
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DELETE FROM StrategySettings WHERE SettingKey LIKE 'PutRsi%' OR SettingKey = 'EnablePutRsiBand2';
PRINT CONCAT('Rows deleted: ', @@ROWCOUNT, ' (expected 6)');
COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT COUNT(*) AS RowsLeft FROM StrategySettings WHERE SettingKey LIKE 'PutRsi%' OR SettingKey = 'EnablePutRsiBand2';   -- expect 0
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK (re-create the rows; the removed code must be restored for them to do anything) -------------------------
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnablePutRsiBand2', NULL, 0.000000, 1, N'Enable second PUT RSI band globally (0 = off).', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnablePutRsiBand2', N'NIFTY', 0.000000, 1, N'Enable second PUT RSI band for NIFTY (0 = off).', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'PutRsiBand2Ceiling', NULL, 49.000000, 1, N'Second PUT RSI band ceiling.', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'PutRsiBand2Floor', NULL, 40.000000, 1, N'Second PUT RSI band floor (OR-ed with PutRsiFloor..Ceiling).', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'PutRsiCeiling', NULL, 38.000000, 0, N'PUT cond 5: RSI upper bound (was RsiPutCeiling=55)', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'PutRsiFloor', NULL, 28.000000, 1, N'PUT cond 5: RSI lower bound', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
------------------------------------------------------------------------------------------------------------------------ */
