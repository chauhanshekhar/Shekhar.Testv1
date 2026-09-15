/*
================================================================================
 BANKNIFTY PUT BREAKDOWN — kill switch
================================================================================
The rule now reads EnableBankNiftyPutBreakdownRule from StrategySettings and
DEFAULTS TO ENABLED, so deploying the code changes nothing. You only need a row
here when you want to turn the rule OFF.

Picked up within the settings cache's 5-minute refresh — no restart, no redeploy.

This affects the DIRECTIONAL approved-rule path only. HeroZero is a separate
engine (StrategyManager.RunHeroZero) and is not touched by this key.
================================================================================
*/

/* ---------------------------------------------------------------------------
   TURN THE RULE OFF
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings
           WHERE SettingKey = 'EnableBankNiftyPutBreakdownRule' AND Symbol = 'BANKNIFTY')
    UPDATE dbo.StrategySettings
       SET SettingValue = 0, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableBankNiftyPutBreakdownRule' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableBankNiftyPutBreakdownRule', 'BANKNIFTY', 0, 1,
            'Kill switch for the approved BANKNIFTY PUT BREAKDOWN directional rule. 0 = suppressed.',
            SYSDATETIME());

/* ---------------------------------------------------------------------------
   TURN IT BACK ON  (or simply delete the row — absent means enabled)
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 1, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableBankNiftyPutBreakdownRule' AND Symbol = 'BANKNIFTY';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey IN ('EnableBankNiftyPutBreakdownRule', 'EnableSensexDirectionalRule')
ORDER BY SettingKey;

/* ---------------------------------------------------------------------------
   CONFIRM IT TOOK EFFECT (run a few minutes after the cache refresh)
   Suppressed cycles record the reason in the audit row, so this returns rows
   only once the switch is actually live.
   --------------------------------------------------------------------------- */
SELECT TOP (20) SnapshotTime, Symbol, BlockedBy, BlockedReason
FROM   dbo.IndicatorSnapshots
WHERE  Symbol = 'BANKNIFTY'
  AND  SnapshotTime >= DATEADD(MINUTE, -30, SYSDATETIME())
  AND  BlockedReason LIKE '%EnableBankNiftyPutBreakdownRule%'
ORDER BY SnapshotTime DESC;
