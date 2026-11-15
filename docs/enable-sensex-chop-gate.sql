/*
================================================================================
 SENSEX CHOP GATE - forward trial switch (EnableChopGate)
================================================================================
Gate: ChopGate (Trading.Worker/SignalGates/ChopGate.cs). Blocks a SENSEX entry when
15-bar chop = sum(1m High-Low) / (15-bar High - Low) is above 4.5.
Evidence and caveats: docs/research/2026-11-12-sensex-noise-gate/ranking.md
(HYPOTHESIS, NOT VALIDATED - failed the both-halves rule; forward trial only).

The gate DOES NOT CHECK THE TRADING MODE. It applies in whatever mode SystemSettings
holds (Paper or Live). Run this while the mode is Paper for the trial. Before going Live,
set it to 0 (see ROLLBACK) unless you deliberately want it blocking real entries.

Default is OFF (no row = off). Picked up within the settings cache refresh (~5 min);
the Worker must be deployed first. Executed by the user, never by Claude.
================================================================================
*/

/* ENABLE (SENSEX only) */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol = 'SENSEX')
    UPDATE dbo.StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableChopGate' AND Symbol = 'SENSEX';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableChopGate', 'SENSEX', 1, 1,
            'SENSEX only: block entry when 15-bar 1m chop > 4.5 (forward trial, docs/research/2026-11-12-sensex-noise-gate). Mode-agnostic. 0 = off.',
            SYSDATETIME());

/* ROLLBACK (turn off; do this before going Live unless intended)
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableChopGate' AND Symbol = 'SENSEX';
*/

/* VERIFY the row */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'EnableChopGate';

/* VERIFY it is working (after the cache refresh): setups the gate blocked */
SELECT TOP 50 *
FROM   dbo.IndicatorSnapshots WITH (NOLOCK)
WHERE  BlockedBy = 'ChopGate' AND Symbol = 'SENSEX'
ORDER BY Id DESC;
