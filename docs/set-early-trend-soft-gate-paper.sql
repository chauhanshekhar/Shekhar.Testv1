/*
================================================================================
 PAPER TRIAL - EarlyTrendGate soft rule ON for BANKNIFTY and SENSEX  (EnableEarlyTrendSoftGate)
================================================================================
NOT EXECUTED BY CLAUDE. Run it yourself, ONLY after the new Worker build is deployed (before that the key is simply not read).
Guarded: it refuses to run unless the connected database is TradingDB_Paper. Never run it on a live database.

What it enables (docs/research/2026-11-11-early-trend-gapdays/ATR5M-DESIGN.md): a CALL that EarlyTrendGate would block (5m EMA20 > EMA50)
is allowed when ALL hold: session gap >= 0.30% up or down, IST time >= 10:30, 5m ATR(14) available and fresh, and
(EMA20_5m - EMA50_5m) / ATR5m < 0.30. The gap-up morning exception is unchanged. PUT is unaffected. NIFTY gets NO row (stays off:
absent = off). Evidence is a replay (17 added trades) - PROMISING, NOT validated. Review after ~10 qualifying gap sessions.

Kill switch: set the rows to 0 (ROLLBACK below); picked up within the settings cache refresh (~5 min).
Watch the Worker log for "EarlyTrendGate SOFT_GATE_EVAL" lines (gap, EMAs, ATR5m, normalized gap, decision).
================================================================================
*/

IF DB_NAME() <> 'TradingDB_Paper'
    THROW 50001, 'Refusing to run: connect to TradingDB_Paper (paper trial only).', 1;

/* ---------------------------------------------------------------------------
   ENABLE FOR BANKNIFTY AND SENSEX  (idempotent: UPDATE if the row exists, else INSERT)
   --------------------------------------------------------------------------- */
DECLARE @Symbols TABLE (Symbol nvarchar(50));
INSERT INTO @Symbols VALUES (N'BANKNIFTY'), (N'SENSEX');

UPDATE s
   SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
  FROM dbo.StrategySettings s
  JOIN @Symbols x ON x.Symbol = s.Symbol
 WHERE s.SettingKey = 'EnableEarlyTrendSoftGate';

INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
SELECT 'EnableEarlyTrendSoftGate', x.Symbol, 1, 1,
       'Soft early-trend gate (paper trial): allow a CALL the gate blocks on gap days from 10:30 IST while (EMA20_5m-EMA50_5m)/ATR5m < 0.30. 0 = off.',
       SYSDATETIME()
  FROM @Symbols x
 WHERE NOT EXISTS (SELECT 1 FROM dbo.StrategySettings s
                    WHERE s.SettingKey = 'EnableEarlyTrendSoftGate' AND s.Symbol = x.Symbol);

/* ---------------------------------------------------------------------------
   ROLLBACK  (off again; absent also means off)
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableEarlyTrendSoftGate';

-- or remove the rows:
-- DELETE FROM dbo.StrategySettings WHERE SettingKey = 'EnableEarlyTrendSoftGate';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY  (expect BANKNIFTY and SENSEX rows with SettingValue 1, IsActive 1; no NIFTY row; no Symbol IS NULL row)
   --------------------------------------------------------------------------- */
SELECT Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
  FROM dbo.StrategySettings
 WHERE SettingKey IN ('EnableEarlyTrendSoftGate', 'EnableEarlyTrendGate', 'EnableGapUpMorningException')
 ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   TRIAL REVIEW (after ~10 gap sessions): CALL signals created on gap days after 10:30 that the soft gate let through are the ones with a
   SOFT_GATE_EVAL ... decision=EXEMPT log line at the same minute. Match them to Signals/TradeJournal by Symbol and time.
   --------------------------------------------------------------------------- */
