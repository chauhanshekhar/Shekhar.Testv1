/*
================================================================================
 NoRsiCall SHADOW strategy — enable / disable
================================================================================
CALL entry = the live CALL chain WITHOUT the RSI band. SHADOW ONLY: it never
places an order, never builds a Signal and never takes the position slot. It is
evaluated after the existing strategy has decided, and only writes one
StrategyExecutionLogs row (SelectedStrategy = 'NoRsiCallShadow') per minute in
which its conditions hold. The existing strategy is unaffected.

Needs the Worker build that contains NoRsiCallShadowEvaluator. Picked up within
the settings cache's 5-minute refresh. Default (no row) = OFF.

Evidence: docs/research/2026-10-29-backtest-condition-live-interchange
(replay only, 15 sessions; gain was train-period only - hence shadow).
================================================================================
*/

/* ---------------------------------------------------------------------------
   ENABLE (global row, all symbols)
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings
           WHERE SettingKey = 'EnableNoRsiCall' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableNoRsiCall' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableNoRsiCall', NULL, 1, 1,
            'Shadow-only CALL strategy without the RSI band. Logs would-be entries to StrategyExecutionLogs; never trades.',
            SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK (turn the shadow off)
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableNoRsiCall' AND Symbol IS NULL;
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'EnableNoRsiCall';

/* ---------------------------------------------------------------------------
   CONFIRM IT TOOK EFFECT (after the deploy + cache refresh, during market hours)
   --------------------------------------------------------------------------- */
SELECT TOP (50) CreatedOn, Symbol, MarketState, Reason
FROM   dbo.StrategyExecutionLogs
WHERE  SelectedStrategy = 'NoRsiCallShadow'
ORDER BY CreatedOn DESC;

/* Shadow minutes per symbol per day, and how often the real strategy also fired */
SELECT CAST(CreatedOn AS date) AS TradeDate, Symbol, COUNT(*) AS ShadowMinutes,
       SUM(CASE WHEN Reason LIKE '%RealSignal=none%' THEN 0 ELSE 1 END) AS RealAlsoFired
FROM   dbo.StrategyExecutionLogs
WHERE  SelectedStrategy = 'NoRsiCallShadow'
GROUP BY CAST(CreatedOn AS date), Symbol
ORDER BY TradeDate DESC, Symbol;
