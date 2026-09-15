/*
================================================================================
Lower the BANKNIFTY ATR floor: MinAtrPercent 0.10 -> 0.030
================================================================================
Required for the BANKNIFTY PUT BREAKDOWN rule to be reachable. The rule's own
floor is AtrPct >= 0.030, but AtrGate runs as a PRE-gate and blocks anything
below StrategySettings['MinAtrPercent', 'BANKNIFTY'], which falls back to 0.10
when no row exists — so matches in [0.030, 0.10) never reach the engine.

SCOPE — READ THIS BEFORE RUNNING
--------------------------------------------------------------------------------
The 'MinAtrPercent' key has TWO consumers, and this one row changes both:

  1. AtrGate.cs:12                — pre-gate, applies to EVERY strategy on
                                    BANKNIFTY (Directional AND HeroZero)
  2. SignalEngineService.cs:196   — the normal Directional AND-chain

So this does NOT only enable the new rule. It lowers the ATR floor for all
BANKNIFTY signal generation from 0.10 to 0.030. Only the PUT BREAKDOWN rule was
backtested at 0.030; normal Directional BANKNIFTY signals in the 0.030-0.10 ATR
band are NOT covered by that validation.

Row is scoped to Symbol = 'BANKNIFTY' deliberately. Do NOT insert it with
Symbol = NULL: that is the global fallback and would lower the floor for NIFTY,
SENSEX, FINNIFTY and MIDCAPNIFTY too, none of which were validated.

TAKES EFFECT: StrategySettingsService caches rows in memory and auto-refreshes
every 5 minutes, so no restart is needed. Allow up to 5 minutes.

This script is idempotent and touches exactly one row.
================================================================================
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;

-- ── 1. BEFORE ────────────────────────────────────────────────────────────────
SELECT 'BEFORE' AS Stage, Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'MinAtrPercent'
  AND (Symbol = 'BANKNIFTY' OR Symbol IS NULL);


-- ── 2. APPLY ─────────────────────────────────────────────────────────────────
BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.StrategySettings
               WHERE SettingKey = 'MinAtrPercent' AND Symbol = 'BANKNIFTY')
    BEGIN
        UPDATE dbo.StrategySettings
        SET    SettingValue = 0.030,
               IsActive     = 1,
               Description  = 'BANKNIFTY PUT BREAKDOWN rule floor (backtested at AtrPct >= 0.030)',
               UpdatedOn    = SYSDATETIME()
        WHERE  SettingKey = 'MinAtrPercent'
          AND  Symbol     = 'BANKNIFTY';
    END
    ELSE
    BEGIN
        INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
        VALUES ('MinAtrPercent', 'BANKNIFTY', 0.030, 1,
                'BANKNIFTY PUT BREAKDOWN rule floor (backtested at AtrPct >= 0.030)',
                SYSDATETIME());
    END

COMMIT TRANSACTION;


-- ── 3. VERIFY ────────────────────────────────────────────────────────────────
-- Expect exactly one BANKNIFTY row, SettingValue = 0.030, IsActive = 1.
-- Expect NO Symbol IS NULL row for this key (that would be a global override).
SELECT 'AFTER' AS Stage, Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'MinAtrPercent'
  AND (Symbol = 'BANKNIFTY' OR Symbol IS NULL);

-- Confirm no other symbol was affected.
SELECT 'OTHER SYMBOLS' AS Stage, Symbol, SettingValue, IsActive
FROM   dbo.StrategySettings
WHERE  SettingKey = 'MinAtrPercent'
  AND  ISNULL(Symbol, '(global)') <> 'BANKNIFTY';


/*
================================================================================
ROLLBACK — restores the previous behaviour (0.10 fallback)
================================================================================
Deactivating rather than deleting keeps the audit trail; the resolver ignores
inactive rows and falls through to the caller default of 0.10.

    UPDATE dbo.StrategySettings
    SET    IsActive  = 0,
           UpdatedOn = SYSDATETIME()
    WHERE  SettingKey = 'MinAtrPercent'
      AND  Symbol     = 'BANKNIFTY';

If the row existed before this change with a different value, restore that value
instead of deactivating — see the BEFORE result above.

Rollback also takes up to 5 minutes to reach the workers.
================================================================================
*/
