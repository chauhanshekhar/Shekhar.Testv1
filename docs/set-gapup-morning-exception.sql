/* EnableGapUpMorningException - EXPERIMENTAL EarlyTrendGate exception (Version D1). REVIEW ONLY: do not run until the code diff is approved
   and deployed. Not executed by Claude.

   HYPOTHESIS, NOT VALIDATED (docs/research/2026-11-04-gapup-morning-exception/REPORT.md): validation improved on 4-5 added trades,
   drawdown rose, and the idea was formed after inspecting the full sample.

   Behaviour when ON (code in EarlyTrendGate): a CALL the gate would block is allowed only if ALL hold:
     - Trading mode is PAPER (Live and HeroZero modes ignore this setting, even when it is 1)
     - session open >= 0.30% above the previous close  (fixed in code, NOT read from OpeningRsiGapPct)
     - IST time strictly before 10:00                   (fixed in code, no setting)
     - 5m EMA20 > 5m EMA50
   Scope: one GLOBAL row (Symbol IS NULL). StrategySettingsService looks up exact Symbol -> Symbol IS NULL -> default, and the gate reads it with
   GetBool(key, ctx.Symbol, false), so this row applies to every symbol. A per-symbol row would override it for that symbol.
   Settings are cached ~5 minutes. */

DECLARE @Enable decimal(18,6) = 0;      -- keep 0 until the diff is approved AND deployed; 1 = on (paper only)

-- APPLY (idempotent, global row)
UPDATE dbo.StrategySettings
SET SettingValue = @Enable, IsActive = 1, UpdatedOn = SYSDATETIME(),
    Description = N'EXPERIMENTAL: allow CALL past EarlyTrendGate on gap-up (>=0.30%) mornings before 10:00 IST, PAPER mode only'
WHERE SettingKey = N'EnableGapUpMorningException' AND Symbol IS NULL;

IF @@ROWCOUNT = 0
    INSERT dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (N'EnableGapUpMorningException', NULL, @Enable, 1,
            N'EXPERIMENTAL: allow CALL past EarlyTrendGate on gap-up (>=0.30%) mornings before 10:00 IST, PAPER mode only', SYSDATETIME());

/* ROLLBACK (turn it off; the row can stay)
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
WHERE SettingKey = N'EnableGapUpMorningException' AND Symbol IS NULL;
-- or remove it: DELETE FROM dbo.StrategySettings WHERE SettingKey = N'EnableGapUpMorningException' AND Symbol IS NULL;
*/

-- VERIFY 1: the switch is OFF (SettingValue 0) and there is no per-symbol override.
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings
WHERE SettingKey = N'EnableGapUpMorningException';

-- VERIFY 2 (system mode): the exception only works in Paper. Never SELECT * from SystemSettings; read this one key only.
SELECT [Key], [Value] FROM dbo.SystemSettings WHERE [Key] = N'TradingMode';

/* FORWARD TEST 1 - exception-ELIGIBLE minutes, from stored snapshots (SnapshotTime is IST). The gate also writes an INFO log line
   "EarlyTrendGate GAPUP_MORNING_EXCEPTION_ELIGIBLE {Symbol} ... gap= 5mEMA20= 5mEMA50= entry=" for each CALL it let through.
   A snapshot row matching these conditions is where the live gate would normally have blocked a CALL. */
DECLARE @From date = '2026-11-05';      -- first session with the switch on
SELECT SnapshotTime, Symbol, DayOpen, YesterdayClose,
       g.GapPct, Ema20_5m, Ema50_5m, SignalGenerated, BlockedBy, BlockedReason
FROM dbo.IndicatorSnapshots
CROSS APPLY (SELECT (DayOpen - YesterdayClose) / NULLIF(YesterdayClose, 0) * 100.0 AS GapPct) g
WHERE SnapshotTime >= @From
  AND CAST(SnapshotTime AS time) < '10:00:00'
  AND g.GapPct >= 0.30
  AND Ema20_5m > Ema50_5m AND Ema50_5m > 0
  AND SignalGenerated = 1
ORDER BY SnapshotTime;

/* FORWARD TEST 2 - ACTUAL entries: CALL signals created before 10:00 on gap-up sessions with the 5m trend already up. The gate cannot see
   later gates, so an eligible log line is not an entry; a row here is a real signal that passed everything after the exception.
   Compare each row with the ELIGIBLE log line (same Symbol, same minute). */
SELECT s.Id, s.Symbol, s.SignalType, s.CreatedOn, s.EntryPrice, s.Status, s.ExitReason
FROM dbo.Signals s
JOIN dbo.IndicatorSnapshots i
  ON i.Symbol = s.Symbol
 AND i.SnapshotTime >= DATEADD(minute, -1, s.CreatedOn) AND i.SnapshotTime <= s.CreatedOn
CROSS APPLY (SELECT (i.DayOpen - i.YesterdayClose) / NULLIF(i.YesterdayClose, 0) * 100.0 AS GapPct) g
WHERE s.SignalType = N'CALL' AND s.CreatedOn >= @From
  AND CAST(s.CreatedOn AS time) < '10:00:00'
  AND g.GapPct >= 0.30 AND i.Ema20_5m > i.Ema50_5m AND i.Ema50_5m > 0
ORDER BY s.CreatedOn;
