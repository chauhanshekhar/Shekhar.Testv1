-- Turn off the Gemini opening/gap rules and the Gemini own position slot.
-- Their code was removed from the Worker on 2026-10-30; this keeps the database consistent with it, so a rollback of the
-- code cannot silently bring them back. HeroZero and every other key are untouched.
-- Run it BEFORE the Worker deploy. Claude did not execute this script.
-- Idempotent: rows already at 0 are left alone.
-- Values before this script (read 2026-10-30): all six keys exist only as global rows (Symbol NULL), SettingValue 1, IsActive 1.

SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE StrategySettings
SET    SettingValue = 0, UpdatedOn = SYSUTCDATETIME()
WHERE  SettingKey IN ('EnableGeminiNormalOrb', 'EnableGeminiGapUpGo', 'EnableGeminiGapUpFade',
                      'EnableGeminiGapDownContinuation', 'EnableGeminiGapDownRebound', 'GeminiIgnoreOnePosition')
  AND  SettingValue <> 0;

SELECT @@ROWCOUNT AS RowsTurnedOff;   -- expected 6

COMMIT;

-- VERIFY: every row below should show SettingValue = 0.
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   StrategySettings
WHERE  SettingKey LIKE 'Enable%Gemini%' OR SettingKey = 'GeminiIgnoreOnePosition'
ORDER  BY SettingKey, Symbol;

-- ROLLBACK (only together with restoring the removed code):
-- UPDATE StrategySettings SET SettingValue = 1, UpdatedOn = SYSUTCDATETIME()
-- WHERE  Symbol IS NULL
--   AND  SettingKey IN ('EnableGeminiNormalOrb', 'EnableGeminiGapUpGo', 'EnableGeminiGapUpFade',
--                       'EnableGeminiGapDownContinuation', 'EnableGeminiGapDownRebound', 'GeminiIgnoreOnePosition');
