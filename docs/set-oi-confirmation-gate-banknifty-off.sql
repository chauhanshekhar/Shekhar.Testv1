/*
================================================================================
 BANKNIFTY — turn the OI confirmation gate OFF  (EnableOiConfirmationGate)
================================================================================
NOT EXECUTED BY CLAUDE. Run it yourself.

OiConfirmationGate (src/Trading.Worker/SignalGates/OiConfirmationGate.cs) rejects a built signal with
"OI does not confirm CALL on BANKNIFTY (ATM+-2) - signal suppressed" unless the last closed 1m candle moved
in the trade's direction AND the ATM+-2 OI change agrees (long build-up / short covering). It re-checks
every cycle, so a built setup waits until a later minute confirms.

The switch already exists and needs NO code change and NO redeploy:
  key    = EnableOiConfirmationGate
  scope  = per symbol (this script writes a BANKNIFTY row only; no Symbol IS NULL row, so NIFTY and
           SENSEX are already OFF)
  absent = ON (today's behaviour)  |  value 0 = gate skipped for that symbol
Picked up within the settings cache's 5-minute refresh.

Evidence (docs/research/2026-11-11-oi-gate-test/backtest, 2026-10-07..11-11, ~20 sessions with option history,
costed Rs20/order + 1pt): BANKNIFTY gate ON vs OFF. CALL: +21.3k (n=43, PF 1.69) vs +19.3k (n=47, PF 1.57) - a wash.
PUT: -38.5k (n=80, PF 0.89) vs -31.5k (n=87, PF 0.98). Portfolio: CALL -3.7k -> +0.6k, PUT -15.8k -> -13.3k.
The difference is a few thousand over ~20 sessions, mixed across train/validation/test: weak evidence, no loss shown
from removing the gate. BANKNIFTY PUT loses either way. Test on PAPER first, compare ~10 sessions.

Turning the gate off removes ONE protection from the post-gate chain; every other gate (duplicate,
risk, index-stop floor, stale entry, early-trend, R:R, spread, one-position) still runs.
================================================================================
*/

/* ---------------------------------------------------------------------------
   TURN THE GATE OFF FOR BANKNIFTY  (idempotent: UPDATE if the row exists, else INSERT)
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings
           WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'BANKNIFTY')
    UPDATE dbo.StrategySettings
       SET SettingValue = 0, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableOiConfirmationGate', 'BANKNIFTY', 0, 1,
            'OI confirmation gate for BANKNIFTY. 0 = skipped (setups no longer wait for a 1m candle + ATM+-2 OI to confirm), 1 = on.',
            SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK  (back to ON; deleting the row also means ON because absent = ON)
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 1, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'BANKNIFTY';

-- or remove the row:
-- DELETE FROM dbo.StrategySettings
--  WHERE SettingKey = 'EnableOiConfirmationGate' AND Symbol = 'BANKNIFTY';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY  (expect one BANKNIFTY row, SettingValue 0, IsActive 1; no row with Symbol IS NULL)
   --------------------------------------------------------------------------- */
SELECT Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'EnableOiConfirmationGate'
ORDER BY Id DESC;

/* ---------------------------------------------------------------------------
   CONFIRM IT TOOK EFFECT (a few minutes after the cache refresh): this should return no new
   BANKNIFTY rows once the gate is off.
   --------------------------------------------------------------------------- */
SELECT TOP (20) SnapshotTime, Symbol, BlockedBy, BlockedReason
FROM   dbo.IndicatorSnapshots
WHERE  Symbol = 'BANKNIFTY'
  AND  SnapshotTime >= DATEADD(MINUTE, -30, SYSDATETIME())
  AND  BlockedReason LIKE 'OI does not confirm%'
ORDER BY SnapshotTime DESC;
