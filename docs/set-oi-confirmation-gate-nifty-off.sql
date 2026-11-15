/*
================================================================================
 NIFTY — turn the OI confirmation gate OFF  (EnableOiConfirmationGate)
================================================================================
NOT EXECUTED BY CLAUDE. Run it yourself.

OiConfirmationGate (src/Trading.Worker/SignalGates/OiConfirmationGate.cs) rejects a built signal with
"OI does not confirm CALL on NIFTY (ATM+-2) - signal suppressed" unless the last closed 1m candle moved
in the trade's direction AND the ATM+-2 OI change agrees (long build-up / short covering). It re-checks
every cycle, so a built setup waits until a later minute confirms.

The switch already exists and needs NO code change and NO redeploy:
  key    = EnableOiConfirmationGate
  scope  = per symbol (this script writes a NIFTY row only; no Symbol IS NULL row, so BANKNIFTY and
           SENSEX keep the gate ON)
  absent = ON (today's behaviour)  |  value 0 = gate skipped for that symbol
Picked up within the settings cache's 5-minute refresh.

Evidence (docs/research/2026-10-05-late-entry, 11 sessions, UNCOSTED): setups the gate rejected replayed
as profitably as the ones it passed (CALL rejected +146,047 PF 2.04 vs passed +102,224; PUT rejected
+41,021 vs passed +32,610); per symbol it is mixed (helped NIFTY PUT, hurt SENSEX). This is a lead, not
proof, so test on PAPER first and compare NIFTY trades before and after over ~10 sessions, costs applied.

Turning the gate off removes ONE protection from the post-gate chain; every other gate (duplicate,
risk, index-stop floor, stale entry, early-trend, R:R, spread, one-position) still runs.
================================================================================
*/

/* ---------------------------------------------------------------------------
   TURN THE GATE OFF FOR NIFTY  (idempotent: UPDATE if the row exists, else INSERT)
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings
           WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'NIFTY')
    UPDATE dbo.StrategySettings
       SET SettingValue = 0, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'NIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableOiConfirmationGate', 'NIFTY', 0, 1,
            'OI confirmation gate for NIFTY. 0 = skipped (setups no longer wait for a 1m candle + ATM+-2 OI to confirm), 1 = on.',
            SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK  (back to ON; deleting the row also means ON because absent = ON)
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 1, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'NIFTY';

-- or remove the row:
-- DELETE FROM dbo.StrategySettings
--  WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'NIFTY';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY  (expect one NIFTY row, SettingValue 0, IsActive 1; no row with Symbol IS NULL)
   --------------------------------------------------------------------------- */
SELECT Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'EnableOiConfirmationGate'
ORDER BY Id DESC;

/* ---------------------------------------------------------------------------
   CONFIRM IT TOOK EFFECT (a few minutes after the cache refresh): this should return no new
   NIFTY rows once the gate is off.
   --------------------------------------------------------------------------- */
SELECT TOP (20) SnapshotTime, Symbol, BlockedBy, BlockedReason
FROM   dbo.IndicatorSnapshots
WHERE  Symbol = 'NIFTY'
  AND  SnapshotTime >= DATEADD(MINUTE, -30, SYSDATETIME())
  AND  BlockedReason LIKE 'OI does not confirm%'
ORDER BY SnapshotTime DESC;
