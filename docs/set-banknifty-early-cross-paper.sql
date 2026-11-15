/*
================================================================================
 BANKNIFTY EARLY-CROSS CALL — paper-only trial (EnableEarlyCrossCall)
================================================================================
NOT EXECUTED BY CLAUDE. Run it yourself after the Worker with EarlyCrossCallRuleEvaluator is deployed.

The rule (src/Trading.SignalEngine/Strategies/EarlyCrossCallRuleEvaluator.cs) ships OFF: with no rows
it never matches. It is also PAPER-ONLY IN CODE: if TradingMode is Live or HeroZero it returns no
match even with these rows present. Only BANKNIFTY rows are created (no Symbol IS NULL row), so
NIFTY and SENSEX stay off.

Picked up within the settings cache's 5-minute refresh - no restart, no redeploy.

Evidence (replay, after Rs20/order + 1pt costs, docs/research/2026-10-27-early-cross/PROPOSAL.md):
41 trades, +38,789, win 68%, PF 2.8; train +17,266 / validation -1,223 (8 trades) / test +38,006.
UNVALIDATED LIVE. Evaluate on paper for 15+ sessions / 30+ trades; revert if net after costs is
negative at 20 trades.

Signals from this rule carry Signals.SelectedStrategy = 'EarlyCross' (see the EVALUATE query below).

It reuses existing keys already in StrategySettings: CallRsiFloor / CallRsiCeiling (the 5m RSI band)
and EmaGapMinPoints (BANKNIFTY row). Do not change them here.
================================================================================
*/

/* ---------------------------------------------------------------------------
   TURN THE PAPER TRIAL ON  (idempotent: UPDATE if the row exists, else INSERT)
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableEarlyCrossCall' AND Symbol = 'BANKNIFTY')
    UPDATE dbo.StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableEarlyCrossCall' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableEarlyCrossCall', 'BANKNIFTY', 1, 1,
            'Paper-only early-cross CALL: enter within EarlyCrossMaxBars minutes of the 1m EMA20>EMA50 cross. 0 = off.',
            SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EarlyCrossMaxBars' AND Symbol = 'BANKNIFTY')
    UPDATE dbo.StrategySettings
       SET SettingValue = 10, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EarlyCrossMaxBars' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EarlyCrossMaxBars', 'BANKNIFTY', 10, 1,
            'Closed 1m bars after the EMA20>EMA50 cross within which the early-cross CALL may enter (tested: 10).',
            SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EarlyCrossRequire5mNotUp' AND Symbol = 'BANKNIFTY')
    UPDATE dbo.StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EarlyCrossRequire5mNotUp' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EarlyCrossRequire5mNotUp', 'BANKNIFTY', 1, 1,
            'Early-cross CALL only while the 5m EMA20 is not above the 5m EMA50 (tested: 1).',
            SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EarlyCrossRequireEmaGap' AND Symbol = 'BANKNIFTY')
    UPDATE dbo.StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EarlyCrossRequireEmaGap' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EarlyCrossRequireEmaGap', 'BANKNIFTY', 1, 1,
            'Early-cross CALL keeps the live EmaGapMinPoints rule (tested: 1).',
            SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK  (turns the rule off; the extra rows are harmless once the flag is 0)
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableEarlyCrossCall' AND Symbol = 'BANKNIFTY';

-- Full removal (optional):
-- DELETE FROM dbo.StrategySettings
--  WHERE Symbol = 'BANKNIFTY'
--    AND SettingKey IN ('EnableEarlyCrossCall','EarlyCrossMaxBars','EarlyCrossRequire5mNotUp','EarlyCrossRequireEmaGap');
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey IN ('EnableEarlyCrossCall','EarlyCrossMaxBars','EarlyCrossRequire5mNotUp','EarlyCrossRequireEmaGap',
                      'CallRsiFloor','CallRsiCeiling','EmaGapMinPoints')
  AND  (Symbol IS NULL OR Symbol = 'BANKNIFTY')
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   EVALUATE (after sessions have run in PAPER mode): early-cross trades only
   --------------------------------------------------------------------------- */
SELECT TOP (50) s.Id, s.CreatedOn, s.Symbol, s.SignalType, s.Status, s.ExitReason, s.OptionLtp
FROM   dbo.Signals s
WHERE  s.SelectedStrategy = 'EarlyCross'
ORDER BY s.Id DESC;
