/* FastEmaGate settings (EMA9 > EMA21 for CALL, gap-up sessions only by default).
   Code ships with the gate OFF (EnableFastEmaGate absent = off). NOT executed by Claude - run it yourself.

   Evidence is thin (docs/research/2026-11-04-fast-ema-9-21): 3 stop-outs removed on 40 gap-up CALL trades, holdout unchanged.
   Enable for PAPER trading first and re-test on stored IndicatorSnapshots.Ema9/Ema21 after ~10 sessions.
   Requires the Worker build containing FastEmaGate and the migration AddIndicatorSnapshotEma9Ema21 (docs/add-indicatorsnapshot-ema9-ema21.sql).
   Settings are cached ~5 minutes. */

DECLARE @Enable decimal(18,6) = 0;      -- set to 1 to turn the gate on

-- APPLY (idempotent, global rows)
DECLARE @Rows TABLE (K nvarchar(100), V decimal(18,6), D nvarchar(400));
INSERT @Rows VALUES
 (N'EnableFastEmaGate',    @Enable, N'1 = block CALL while 1m EMA9 <= EMA21 (gap-up sessions by default)'),
 (N'FastEmaGateSide',      1,       N'0 = CALL and PUT, 1 = CALL only, 2 = PUT only'),
 (N'FastEmaGateGapUpOnly', 1,       N'1 = only when the session opened up by at least OpeningRsiGapPct');

UPDATE s SET s.SettingValue = r.V, s.IsActive = 1, s.Description = r.D, s.UpdatedOn = SYSDATETIME()
FROM dbo.StrategySettings s JOIN @Rows r ON s.SettingKey = r.K AND s.Symbol IS NULL;

INSERT dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
SELECT r.K, NULL, r.V, 1, r.D, SYSDATETIME()
FROM @Rows r
WHERE NOT EXISTS (SELECT 1 FROM dbo.StrategySettings s WHERE s.SettingKey = r.K AND s.Symbol IS NULL);

/* ROLLBACK (turns the gate off; the rows can stay)
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
WHERE SettingKey = N'EnableFastEmaGate' AND Symbol IS NULL;
-- or remove the rows entirely:
-- DELETE FROM dbo.StrategySettings WHERE SettingKey IN (N'EnableFastEmaGate', N'FastEmaGateSide', N'FastEmaGateGapUpOnly') AND Symbol IS NULL;
*/

-- VERIFY
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings
WHERE SettingKey IN (N'EnableFastEmaGate', N'FastEmaGateSide', N'FastEmaGateGapUpOnly')
ORDER BY SettingKey;

-- After enabling, blocks show up here:
-- SELECT TOP (20) SnapshotTime, Symbol, BlockedBy, BlockedReason FROM dbo.IndicatorSnapshots WHERE BlockedBy LIKE N'%FastEma%' ORDER BY Id DESC;
