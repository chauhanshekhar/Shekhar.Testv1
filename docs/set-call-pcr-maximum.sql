/* ============================================================================
   CallPcrMaximum = 1.5 (CALL only, global)                           2026-10-26
   ----------------------------------------------------------------------------
   Condition 12b in SignalEngineService / CallEntryEvaluator: CALL entry needs
   PCR <= CallPcrMaximum. There is no Enable flag: the condition applies exactly
   while this row exists and IsActive = 1 (an unconfigured key is off).
   PUT is not affected (PutPcrMaximum is a separate key).
   Claude does not execute this file (.claude/rules/02-approval-gate.md).
   No backtest was run for a CALL PCR ceiling; 1.5 is a loose cap (user's value).
   Settings cache refreshes ~5 minutes; no restart needed, but the Worker/API
   must be deployed with the 12b code first or the row is ignored.
   ============================================================================ */

SET NOCOUNT ON;
GO

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'CallPcrMaximum' AND Symbol IS NULL)
    UPDATE StrategySettings
       SET SettingValue = 1.5, IsActive = 1, UpdatedOn = SYSUTCDATETIME(),
           Description = 'CALL entry requires PCR <= this (condition 12b). Global.'
     WHERE SettingKey = 'CallPcrMaximum' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('CallPcrMaximum', NULL, 1.5, 1, 'CALL entry requires PCR <= this (condition 12b). Global.', SYSUTCDATETIME());
GO

-- VERIFY
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WHERE SettingKey = 'CallPcrMaximum';

/* ROLLBACK / KILL SWITCH (turns the condition off; removes nothing):
UPDATE StrategySettings SET IsActive = 0, UpdatedOn = SYSUTCDATETIME()
 WHERE SettingKey = 'CallPcrMaximum' AND Symbol IS NULL;
*/
