/*
================================================================================
 RATED DIRECTION (no RSI) SHADOW — enable / disable
================================================================================
Scores CALL and PUT 0-100 each minute (trend, EMA gap, momentum, PCR, ATR - no
RSI) and records the winning side. SHADOW ONLY: never places an order, never
builds a Signal, never takes the position slot; evaluated after the existing
strategy decided. Rows: StrategyExecutionLogs, SelectedStrategy = 'RatedDirShadow'.

Thresholds from docs/research/2026-10-29-rated-direction (10-07..10-28, live exit
replay, after costs): CALL score >= 65, margin >= 30 -> +47,673 over 67 trades,
positive in train/val/holdout; PUT had no edge at any threshold.

Needs the Worker build that contains RatedDirectionScorer. Default (no row) = OFF.
================================================================================
*/

/* ---------------------------------------------------------------------------
   ENABLE + THRESHOLDS (global rows)
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableRatedDirection' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableRatedDirection' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableRatedDirection', NULL, 1, 1, 'Shadow-only no-RSI CALL vs PUT score strategy. Logs winners; never trades.', SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'RatedMinScore' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 65, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'RatedMinScore' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('RatedMinScore', NULL, 65, 1, 'Rated direction: minimum winning score (0-100).', SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'RatedMinMargin' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 30, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'RatedMinMargin' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('RatedMinMargin', NULL, 30, 1, 'Rated direction: winner must beat the other side by this many points.', SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK (turn the shadow off; thresholds are inert without the flag)
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableRatedDirection' AND Symbol IS NULL;
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey IN ('EnableRatedDirection', 'RatedMinScore', 'RatedMinMargin');

/* CONFIRM (after deploy + 5-min cache refresh, market hours) */
SELECT TOP (50) CreatedOn, Symbol, Score, Reason
FROM   dbo.StrategyExecutionLogs
WHERE  SelectedStrategy = 'RatedDirShadow'
ORDER BY CreatedOn DESC;

SELECT CAST(CreatedOn AS date) AS TradeDate, Symbol,
       SUM(CASE WHEN Reason LIKE 'SHADOW CALL%' THEN 1 ELSE 0 END) AS CallMinutes,
       SUM(CASE WHEN Reason LIKE 'SHADOW PUT%'  THEN 1 ELSE 0 END) AS PutMinutes
FROM   dbo.StrategyExecutionLogs
WHERE  SelectedStrategy = 'RatedDirShadow'
GROUP BY CAST(CreatedOn AS date), Symbol
ORDER BY TradeDate DESC, Symbol;
