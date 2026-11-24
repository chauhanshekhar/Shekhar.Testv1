/* =====================================================================================================================
   enable-close-breakout-banknifty-put.sql
   Turns CloseBreakoutGate on for BANKNIFTY PUT only:
     - EnableCloseBreakoutGate  = 1 for Symbol 'BANKNIFTY' (the global row stays 0, so NIFTY and SENSEX are unaffected);
     - CloseBreakoutGateSide    = 2 (PUT only) as a global row; it is harmless on symbols where the gate is off.
   The gate lookback is BreakoutLookbackCandles (existing row, 5).
   REQUIRES the Worker build that registers CloseBreakoutGate in SignalWorker.PostGates and adds CloseBreakoutGateSide.
   Evidence (docs/research, 2026-11-24): BANKNIFTY PUT blocked trades lost -20.5k (current settings) / -33.6k (broad), permutation
   p 0.07 / 0.06, post-hoc subgroup. BANKNIFTY CALL would be hurt, hence the side switch. Shadow-test before trusting it.
   NOT executed by Claude - review, then run it yourself.
   ===================================================================================================================== */
SET XACT_ABORT ON;
DECLARE @Now datetime2 = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IST, as IstClock.Now

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'CloseBreakoutGateSide' AND Symbol IS NULL)
    UPDATE StrategySettings SET SettingValue = 2, IsActive = 1, UpdatedOn = @Now
     WHERE SettingKey = 'CloseBreakoutGateSide' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('CloseBreakoutGateSide', NULL, 2, 1, 'CloseBreakoutGate side: 0 = CALL and PUT, 1 = CALL only, 2 = PUT only.', @Now);

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'EnableCloseBreakoutGate' AND Symbol = 'BANKNIFTY')
    UPDATE StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = @Now
     WHERE SettingKey = 'EnableCloseBreakoutGate' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableCloseBreakoutGate', 'BANKNIFTY', 1, 1,
            'CloseBreakoutGate for BANKNIFTY only (with CloseBreakoutGateSide = 2: PUT only). 1 = on.', @Now);

COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn FROM StrategySettings
 WHERE SettingKey IN ('EnableCloseBreakoutGate', 'CloseBreakoutGateSide', 'BreakoutLookbackCandles') ORDER BY SettingKey, Symbol;
-- expect: EnableCloseBreakoutGate NULL=0 and BANKNIFTY=1; CloseBreakoutGateSide NULL=2; BreakoutLookbackCandles 5
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK (switch the gate back off for BANKNIFTY; side row is harmless) ------------------------------------------
UPDATE StrategySettings SET SettingValue = 0, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
 WHERE SettingKey = 'EnableCloseBreakoutGate' AND Symbol = 'BANKNIFTY';
-- optional: DELETE FROM StrategySettings WHERE SettingKey = 'CloseBreakoutGateSide' AND Symbol IS NULL;
------------------------------------------------------------------------------------------------------------------------ */
