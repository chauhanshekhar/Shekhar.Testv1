-- Turn off Rated CALL and NoRsiCall (trading, shadow logging and own position slots).
-- Their code was removed from the Worker on 2026-10-30; this keeps the database consistent with it, so a rollback of the
-- code cannot silently bring them back. HeroZero and every other key are untouched.
-- Run it BEFORE the Worker deploy. Claude did not execute this script.
-- Idempotent: setting a value that is already 0 is harmless. Only global rows (Symbol IS NULL) were created on 10-29.

SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE StrategySettings
SET    SettingValue = 0, UpdatedOn = SYSUTCDATETIME()
WHERE  SettingKey IN ('EnableRatedCallTrading', 'RatedOwnPositionSlot', 'EnableRatedDirection',
                      'EnableNoRsiCallTrading', 'NoRsiOwnPositionSlot', 'EnableNoRsiCall')
  AND  SettingValue <> 0;

SELECT @@ROWCOUNT AS RowsTurnedOff;

COMMIT;

-- VERIFY: every row below should show SettingValue = 0.
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   StrategySettings
WHERE  SettingKey IN ('EnableRatedCallTrading', 'RatedOwnPositionSlot', 'EnableRatedDirection', 'RatedMinScore', 'RatedMinMargin',
                      'EnableNoRsiCallTrading', 'NoRsiOwnPositionSlot', 'EnableNoRsiCall')
ORDER  BY SettingKey, Symbol;

-- ROLLBACK (only together with restoring the removed code): values as they were on 2026-10-29.
-- UPDATE StrategySettings SET SettingValue = 1, UpdatedOn = SYSUTCDATETIME()
-- WHERE  Symbol IS NULL
--   AND  SettingKey IN ('EnableRatedCallTrading', 'RatedOwnPositionSlot',
--                       'EnableNoRsiCallTrading', 'NoRsiOwnPositionSlot', 'EnableNoRsiCall');
-- EnableRatedDirection: restore whatever the VERIFY query showed before this script ran.
