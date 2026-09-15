/* ============================================================================
   DELETE ORPHANED StrategySettings ROWS                       2026-09-12
   ----------------------------------------------------------------------------
   These 13 SettingKeys are no longer read by ANY code in the working tree.
   They were verified by grepping every GetBool/GetDecimal/GetInt/IsSettingActive
   call site against the live table (64 keys read by code, 71 in the DB).

   ROLLBACK: docs/restore-deleted-strategysettings.sql regenerates every row
   exactly as it was (value, IsActive, description) at the moment of deletion.

   NOT INCLUDED - see the warning at the bottom of this file:
     EnablePriceChangeFilter
   ========================================================================== */

BEGIN TRAN;

-- What is about to go (run this first and eyeball it)
SELECT SettingKey, Symbol, SettingValue, IsActive, Description
FROM dbo.StrategySettings
WHERE SettingKey IN (
    'DeltaMinimum',
    'EnableCallStrongTrendFilter',
    'EnablePcrFilter',
    'EnableSensexDirectionalRule',
    'ExpiryDaySingleCutoffHour',
    'FixedIndexPoints',
    'MinSignalConfidence',
    'OptionStopLossPercent',
    'PcrBearishThreshold',
    'PcrBullishThreshold',
    'ProtectedSlBreakevenBufferPercent',
    'ProtectedSlMinMovePercent',
    'ProtectedSlTrailFactor')
ORDER BY SettingKey;

DELETE FROM dbo.StrategySettings
WHERE SettingKey IN (
    'DeltaMinimum',
    'EnableCallStrongTrendFilter',
    'EnablePcrFilter',
    'EnableSensexDirectionalRule',
    'ExpiryDaySingleCutoffHour',
    'FixedIndexPoints',
    'MinSignalConfidence',
    'OptionStopLossPercent',
    'PcrBearishThreshold',
    'PcrBullishThreshold',
    'ProtectedSlBreakevenBufferPercent',
    'ProtectedSlMinMovePercent',
    'ProtectedSlTrailFactor');

-- Expect exactly 13. Anything else = stop and investigate.
SELECT @@ROWCOUNT AS RowsDeleted;

-- COMMIT;     <-- uncomment after RowsDeleted shows 13
-- ROLLBACK;

/* ----------------------------------------------------------------------------
   HELD BACK:  EnablePriceChangeFilter  (global, SettingValue = 0, IsActive = 1)

   The working tree no longer reads this key, so it is an orphan there. But the
   build RUNNING IN PRODUCTION RIGHT NOW still reads it, and its compiled default
   is TRUE. Deleting the row removes the 0 that is currently holding the filter
   OFF, so the live worker would switch the price-change filter ON within one
   cache cycle (5 minutes) and start rejecting entries it accepts today.

   Delete it only AFTER the new build is deployed:

   DELETE FROM dbo.StrategySettings WHERE SettingKey = 'EnablePriceChangeFilter';
   -------------------------------------------------------------------------- */
