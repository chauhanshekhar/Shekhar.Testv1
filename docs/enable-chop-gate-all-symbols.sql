/*
================================================================================
 CHOP GATE - enable for ALL symbols (EnableChopGate, global row)
================================================================================
Gate: ChopGate (Trading.Worker/SignalGates/ChopGate.cs). Blocks an entry when
15-bar chop = sum(1m High-Low) / (15-bar High - Low) is above 4.5. HeroZero exempt.

EVIDENCE WARNING: the 4.5 cutoff was fitted on 141 SENSEX trades only and failed the
both-halves rule (docs/research/2026-11-12-sensex-noise-gate/ranking.md). NIFTY and
BANKNIFTY are UNTESTED. This is a forward trial. Watch CHOP_GATE_EVAL log lines.

Prerequisite: the Worker build that removes the SENSEX-only restriction must be deployed
first. Before that deploy, NIFTY/BANKNIFTY ignore this row (harmless).

The gate DOES NOT CHECK THE TRADING MODE: it blocks in Paper and Live alike.
Lookup order: exact-symbol row, then this global (Symbol IS NULL) row, then default off.
The existing SENSEX row (value 1) stays as is. Picked up within ~5 min (settings cache).
Executed by the user, never by Claude.
================================================================================
*/

/* ENABLE (global = every symbol) */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableChopGate' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableChopGate', NULL, 1, 1,
            'All symbols: block entry when 15-bar 1m chop > 4.5 (forward trial; evidence SENSEX-only). Mode-agnostic. 0 = off.',
            SYSDATETIME());

/* ROLLBACK - all symbols off (per-symbol rows still apply; SENSEX row stays 1 unless also zeroed)
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableChopGate' AND Symbol IS NULL;
-- optional: also switch SENSEX off
-- UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
--  WHERE SettingKey = 'EnableChopGate' AND Symbol = 'SENSEX';
*/

/* KILL SWITCH for ONE symbol while the global row stays on (example: NIFTY)
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol = 'NIFTY')
    UPDATE dbo.StrategySettings SET SettingValue = 0, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableChopGate' AND Symbol = 'NIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableChopGate', 'NIFTY', 0, 1, 'Chop gate off for NIFTY (overrides global).', SYSDATETIME());
*/

/* VERIFY the rows */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'EnableChopGate';

/* VERIFY it is working (after cache refresh): setups blocked per symbol */
SELECT Symbol, COUNT(*) AS Blocked
FROM   dbo.IndicatorSnapshots WITH (NOLOCK)
WHERE  BlockedBy = 'ChopGate'
GROUP  BY Symbol;
