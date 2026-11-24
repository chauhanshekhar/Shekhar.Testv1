/* =====================================================================================================================
   disable-consecutive-loss-gate-and-prune-orphan-settings.sql
   1) Switches ConsecutiveLossGate off (StrategySettings.EnableConsecutiveLossGate = 0; the gate reads it per symbol, then global,
      default ON - so a global row with 0 turns it off for every symbol).
   2) Deletes 21 StrategySettings keys that no code in src/ (excluding tests) reads. Checked 2026-11-24: no literal of the
      key appears in any .cs file, and none in the Angular sources. Full row contents are in the ROLLBACK block.
   NOT executed by Claude - review, then run it yourself.
   WARNING: with the gate off a live day has no consecutive-loss stop. Restore before Live (see ROLLBACK 1).
   ===================================================================================================================== */
SET XACT_ABORT ON;
DECLARE @Now datetime2 = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IST, as IstClock.Now

BEGIN TRANSACTION;

-- 1) ConsecutiveLossGate off (global row)
IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'EnableConsecutiveLossGate' AND Symbol IS NULL)
    UPDATE StrategySettings SET SettingValue = 0, IsActive = 1, UpdatedOn = @Now
     WHERE SettingKey = 'EnableConsecutiveLossGate' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableConsecutiveLossGate', NULL, 0, 1, 'ConsecutiveLossGate: 1 = on, 0 = off.', @Now);

-- 2) orphan keys (no code reads them)
DELETE FROM StrategySettings
WHERE SettingKey IN (
    N'ConsecutiveLossLivePauseCount',
    N'EnableConsecutiveLossLivePause',
    N'EnableGeminiGapDownContinuation',
    N'EnableGeminiGapDownRebound',
    N'EnableGeminiGapUpFade',
    N'EnableGeminiGapUpGo',
    N'EnableGeminiNormalOrb',
    N'EnableNoRsiCall',
    N'EnableNoRsiCallTrading',
    N'EnablePriceChangeFilter',
    N'GeminiFadeRsiMin',
    N'GeminiGapUpPcrMin',
    N'GeminiIgnoreOnePosition',
    N'GeminiMaxMinutesFromOpen',
    N'GeminiOrMinutes',
    N'GeminiReboundRsiMax',
    N'GeminiRsiBearCeiling',
    N'GeminiRsiBullFloor',
    N'NoRsiOwnPositionSlot',
    N'PartialT1AtrMultiplier',
    N'RatedOwnPositionSlot'
);
PRINT CONCAT('Orphan rows deleted: ', @@ROWCOUNT, ' (expected 21)');

COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn FROM StrategySettings WHERE SettingKey = 'EnableConsecutiveLossGate';
SELECT COUNT(*) AS OrphanRowsLeft FROM StrategySettings WHERE SettingKey IN (
    N'ConsecutiveLossLivePauseCount',
    N'EnableConsecutiveLossLivePause',
    N'EnableGeminiGapDownContinuation',
    N'EnableGeminiGapDownRebound',
    N'EnableGeminiGapUpFade',
    N'EnableGeminiGapUpGo',
    N'EnableGeminiNormalOrb',
    N'EnableNoRsiCall',
    N'EnableNoRsiCallTrading',
    N'EnablePriceChangeFilter',
    N'GeminiFadeRsiMin',
    N'GeminiGapUpPcrMin',
    N'GeminiIgnoreOnePosition',
    N'GeminiMaxMinutesFromOpen',
    N'GeminiOrMinutes',
    N'GeminiReboundRsiMax',
    N'GeminiRsiBearCeiling',
    N'GeminiRsiBullFloor',
    N'NoRsiOwnPositionSlot',
    N'PartialT1AtrMultiplier',
    N'RatedOwnPositionSlot'
);   -- expect 0
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK 1 (turn the gate back on; no data lost) ---------------------------------------------------------------
UPDATE StrategySettings SET SettingValue = 1, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
 WHERE SettingKey = 'EnableConsecutiveLossGate' AND Symbol IS NULL;
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK 2 (re-create the deleted rows exactly as they were; Id values are new, UpdatedOn is the restore time) --
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'ConsecutiveLossLivePauseCount', NULL, 3.000000, 1, N'Consecutive losses that pause live entries for the rest of the IST day', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableConsecutiveLossLivePause', NULL, 1.000000, 1, N'After N consecutive losses today, new entries are paper-only (live paused for the day)', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableGeminiGapDownContinuation', NULL, 0.000000, 1, N'Gemini gap-down continuation PUT on/off', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableGeminiGapDownRebound', NULL, 0.000000, 1, N'Gemini gap-down rebound CALL on/off', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableGeminiGapUpFade', NULL, 0.000000, 1, N'Gemini gap-up fade PUT on/off', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableGeminiGapUpGo', NULL, 0.000000, 1, N'Gemini gap-up go CALL on/off', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableGeminiNormalOrb', NULL, 0.000000, 1, N'Gemini normal-open ORB rule on/off. See docs/enable-gemini-gap-rules.sql', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableNoRsiCall', NULL, 0.000000, 1, N'Shadow-only CALL strategy without the RSI band. Logs would-be entries to StrategyExecutionLogs; never trades.', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnableNoRsiCallTrading', NULL, 0.000000, 1, N'Paper-only CALL chain without the RSI band.', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'EnablePriceChangeFilter', NULL, 0.000000, 1, N'Enable Price Change % filter', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiFadeRsiMin', NULL, 75.000000, 1, N'Gemini gap-up fade PUT: 5m RSI at or above (OR bearish candle)', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiGapUpPcrMin', NULL, 1.100000, 1, N'Gemini gap-up go CALL: minimum PCR', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiIgnoreOnePosition', NULL, 0.000000, 1, N'1 = one open position per strategy (Gemini vs rest) instead of one overall. See docs/set-gemini-ignore-one-position.sql', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiMaxMinutesFromOpen', NULL, 120.000000, 1, N'Gemini: last entry, minutes after the open', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiOrMinutes', NULL, 15.000000, 1, N'Gemini: 1m bars forming the opening range', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiReboundRsiMax', NULL, 35.000000, 1, N'Gemini gap-down rebound CALL: 5m RSI at or below', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiRsiBearCeiling', NULL, 45.000000, 1, N'Gemini normal open PUT: maximum 5m RSI', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'GeminiRsiBullFloor', NULL, 55.000000, 1, N'Gemini normal open CALL: minimum 5m RSI', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'NoRsiOwnPositionSlot', NULL, 0.000000, 1, N'NoRsiCall gets its own position slot beside the existing strategy.', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'PartialT1AtrMultiplier', N'*', 0.500000, 1, NULL, DATEADD(MINUTE, 330, SYSUTCDATETIME()));
INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn) VALUES (N'RatedOwnPositionSlot', NULL, 0.000000, 1, N'Rated CALL gets its own position slot beside the existing strategy.', DATEADD(MINUTE, 330, SYSUTCDATETIME()));
------------------------------------------------------------------------------------------------------------------------ */
