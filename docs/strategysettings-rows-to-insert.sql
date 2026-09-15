/*
================================================================================
StrategySettings rows required by this session's work
================================================================================
Resolution order for any key: exact Symbol match -> Symbol IS NULL (global)
                              -> the default hardcoded at the call site.
So every key below already "works" with no row present; a row only overrides it.

Cache refreshes every 5 minutes (StrategySettingsService) — no restart needed.

  ROW 1  REQUIRED     MinAtrPercent / BANKNIFTY / 0.030
  ROW 2  RECOMMENDED  UseOneMinuteEntryCandle / global / 1
  ROW 3  DO NOT ADD   UseFiveMinuteConfirmation  (backtest first)

Run section 0 first to see what is already there.
================================================================================
*/


/* ── 0. WHAT EXISTS TODAY ─────────────────────────────────────────────────── */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
ORDER  BY SettingKey, Symbol;

-- Specifically: is the ATR floor overridden anywhere? Blank => the 0.10 code default
-- applies, and AtrGate is blocking every symbol (see the note at the bottom).
SELECT SettingKey, Symbol, SettingValue, IsActive
FROM   dbo.StrategySettings
WHERE  SettingKey IN ('MinAtrPercent', 'UseOneMinuteEntryCandle', 'UseFiveMinuteConfirmation');


/* ============================================================================
   ROW 1 — REQUIRED: MinAtrPercent / BANKNIFTY / 0.030
   ============================================================================
   Without this the BANKNIFTY PUT BREAKDOWN rule can NEVER fire. The rule's floor
   is AtrPct >= 0.030, but AtrGate is a PRE-gate that blocks below
   StrategySettings['MinAtrPercent','BANKNIFTY'], defaulting to 0.10 — so every
   match between 0.030 and 0.10 is discarded before the engine runs.

   SCOPE: this key also gates SignalEngineService's own AND-chain, so it lowers
   the ATR floor for ALL BANKNIFTY signal generation, not just the new rule.
   Only the breakdown rule was backtested at 0.030.

   Symbol is 'BANKNIFTY' deliberately — NOT NULL. A global row would lower the
   floor for NIFTY, SENSEX, FINNIFTY and MIDCAPNIFTY, none of which were validated.
   ============================================================================ */

BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.StrategySettings
               WHERE SettingKey = 'MinAtrPercent' AND Symbol = 'BANKNIFTY')
        UPDATE dbo.StrategySettings
        SET    SettingValue = 0.030, IsActive = 1, UpdatedOn = SYSDATETIME(),
               Description  = 'BANKNIFTY PUT BREAKDOWN rule floor (backtested at AtrPct >= 0.030)'
        WHERE  SettingKey = 'MinAtrPercent' AND Symbol = 'BANKNIFTY';
    ELSE
        INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
        VALUES ('MinAtrPercent', 'BANKNIFTY', 0.030, 1,
                'BANKNIFTY PUT BREAKDOWN rule floor (backtested at AtrPct >= 0.030)', SYSDATETIME());

COMMIT TRANSACTION;


/* ============================================================================
   ROW 2 — RECOMMENDED: UseOneMinuteEntryCandle / global / 1
   ============================================================================
   Pins the entry trigger to the 1-minute CLOSED candle in every trading mode.

   Without a row the default is mode-dependent (Paper -> 5m, Live/HeroZero -> 1m
   after the code fix). This row removes that dependency entirely, so the entry
   candle can never change as a side effect of a mode switch.

   Global (Symbol NULL) so all instruments share one trigger.
   ============================================================================ */

BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.StrategySettings
               WHERE SettingKey = 'UseOneMinuteEntryCandle' AND Symbol IS NULL)
        UPDATE dbo.StrategySettings
        SET    SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME(),
               Description  = 'Entry trigger = 1-minute closed candle in all trading modes'
        WHERE  SettingKey = 'UseOneMinuteEntryCandle' AND Symbol IS NULL;
    ELSE
        INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
        VALUES ('UseOneMinuteEntryCandle', NULL, 1, 1,
                'Entry trigger = 1-minute closed candle in all trading modes', SYSDATETIME());

COMMIT TRANSACTION;


/* ============================================================================
   ROW 3 — DO NOT ADD YET: UseFiveMinuteConfirmation
   ============================================================================
   Leave absent so it stays false. Enabling it makes a symbol take NO trades
   until ~11:00 daily (5m EMAs need 20 closed bars, 5m RSI needs 15), because
   the confirmation fails closed.

   Run docs/compare-5m-confirmation-vs-1m-only.sql first. Only if WinRate15
   improves materially without gutting Samples, enable ONE symbol:

       INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
       VALUES ('UseFiveMinuteConfirmation', 'BANKNIFTY', 1, 1,
               '5m confirmation + 1m entry - enabled after backtest', SYSDATETIME());
   ============================================================================ */


/* ── VERIFY ───────────────────────────────────────────────────────────────── */

SELECT 'AFTER' AS Stage, Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey IN ('MinAtrPercent', 'UseOneMinuteEntryCandle', 'UseFiveMinuteConfirmation')
ORDER  BY SettingKey, Symbol;


/* ── ROLLBACK (deactivate; the resolver ignores inactive rows) ─────────────── */
--  UPDATE dbo.StrategySettings SET IsActive = 0, UpdatedOn = SYSDATETIME()
--  WHERE  SettingKey = 'MinAtrPercent' AND Symbol = 'BANKNIFTY';
--
--  UPDATE dbo.StrategySettings SET IsActive = 0, UpdatedOn = SYSDATETIME()
--  WHERE  SettingKey = 'UseOneMinuteEntryCandle' AND Symbol IS NULL;


/*
================================================================================
NOTE — check this before anything else
================================================================================
Your own IndicatorSnapshots screenshot showed:

    NIFTY      AtrPct 0.0253
    SENSEX     AtrPct 0.0244
    BANKNIFTY  AtrPct 0.0365

AtrGate blocks when AtrPct < MinAtrPercent, which defaults to 0.10 when no
StrategySettings row exists. All three values are 3-4x BELOW that.

If section 0 returns no MinAtrPercent rows, AtrGate is currently rejecting
essentially every signal on every symbol, and has been. Confirm with:

    SELECT BlockedBy, COUNT(*)
    FROM   dbo.IndicatorSnapshots
    WHERE  SnapshotTime >= DATEADD(DAY,-1,SYSDATETIME())
    GROUP  BY BlockedBy
    ORDER  BY COUNT(*) DESC;

If 'AtrGate' dominates, that is the single reason you are not getting signals —
and ROW 1 above only fixes BANKNIFTY. NIFTY and SENSEX would each need their own
row, at a value justified by their own ATR distribution.
================================================================================
*/
