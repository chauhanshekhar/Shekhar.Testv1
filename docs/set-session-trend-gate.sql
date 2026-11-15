/* ============================================================================
   SessionTrendGate — enable / tune / kill switch                     2026-10-16
   ----------------------------------------------------------------------------
   The gate ships OFF. Nothing happens until EnableSessionTrendGate = 1.
   Claude does not execute this file (.claude/rules/02-approval-gate.md).

   WHAT IT DOES
   ------------
   Only enters when the session SO FAR is travelling in the trade's direction:
   "gap up and going up and up", "gap down and going down and down", and the
   same on a normal open.

     trendSoFar = (Ltp - DayOpen) / AtrRaw                 CALL wants +, PUT -
     efficiency = |Ltp - DayOpen| / (DayHigh - DayLow)      0 = round-tripped
     offExtreme = distance from the session high/low in ATRs       [optional]

   All three use values known at the entry minute. Code:
   src/Trading.Worker/SignalGates/SessionTrendGate.cs

   *** READ THIS BEFORE ENABLING ***
   On the stored history (68/55/67 sessions, costed at Rs20/order + 1pt
   slippage) every variant of this rule LOST money against the live baseline
   for PUT, and a control that keeps every 4th trade at random beat all of
   them: docs/research/2026-10-16-session-trend/.

     PUT, all symbols, net after costs
       KeepEvery4 (control, no information)   -2,187
       Baseline (live rules)                 -30,455
       SessionTrend, gap-down scope          -24,401
       SessionTrend, every session           -34,511

   The reading is that this system's PUT edge is in entering BEFORE extension,
   not after confirmation - consistent with the EMA20-distance and stretch-cap
   findings. CALL was untestable (5 training trades).

   Those figures assume 1 point of slippage per side. Break-even on the real
   trades is 0.418 points, so the sign of every net figure above depends on
   that assumption - measure real slippage before treating them as settled.

   SAFETY
   ------
   - Idempotent upsert; re-running changes nothing.
   - Touches only SessionTrend* / EnableSessionTrendGate rows.
   - Kill switch and VERIFY at the bottom.
   - Settings cache refreshes ~5 minutes; no restart needed.
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @Symbol  nvarchar(50)  = NULL;   -- NULL = global, or 'BANKNIFTY' etc.
DECLARE @Enable  decimal(18,6) = 0;      -- <<< 1 to turn the gate ON
DECLARE @MinMove decimal(18,6) = 0.75;   -- ATRs the session must have travelled
DECLARE @MinEff  decimal(18,6) = 0.50;   -- 0..1, how one-way that travel must be
DECLARE @NearAtr decimal(18,6) = 0;      -- <= 0 disables the "still at the edge" test
DECLARE @Opening decimal(18,6) = 0;      -- 0 any, 1 gap-up, 2 gap-down, 3 normal
DECLARE @GapPct  decimal(18,6) = 0.30;   -- gap % separating the openings
DECLARE @Side    decimal(18,6) = 0;      -- 0 CALL+PUT, 1 CALL only, 2 PUT only

DECLARE @rows TABLE (SettingKey nvarchar(100), SettingValue decimal(18,6), Descr nvarchar(400));
INSERT INTO @rows VALUES
 (N'EnableSessionTrendGate',    @Enable,  N'Session-trend gate on/off. See docs/set-session-trend-gate.sql'),
 (N'SessionTrendMinMoveAtr',    @MinMove, N'ATRs the session must have travelled in the trade direction'),
 (N'SessionTrendMinEfficiency', @MinEff,  N'|Ltp-DayOpen| / (DayHigh-DayLow) minimum; low = round-tripping'),
 (N'SessionTrendNearExtremeAtr',@NearAtr, N'Max ATRs off the session high/low; <= 0 disables the test'),
 (N'SessionTrendOpening',       @Opening, N'0 any, 1 gap-up, 2 gap-down, 3 normal'),
 (N'SessionTrendGapPct',        @GapPct,  N'Gap percent separating gap-up / normal / gap-down'),
 (N'SessionTrendSide',          @Side,    N'0 CALL and PUT, 1 CALL only, 2 PUT only');

MERGE dbo.StrategySettings AS t
USING @rows AS s
   ON t.SettingKey = s.SettingKey
  AND ISNULL(t.Symbol, N'~') = ISNULL(@Symbol, N'~')
WHEN MATCHED THEN
    UPDATE SET SettingValue = s.SettingValue, IsActive = 1, UpdatedOn = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (s.SettingKey, @Symbol, s.SettingValue, 1, s.Descr, SYSDATETIME());

PRINT CONCAT('SessionTrendGate rows upserted: ', @@ROWCOUNT);
GO

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey = N'EnableSessionTrendGate' OR SettingKey LIKE N'SessionTrend%'
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   KILL SWITCH — turn the gate off, leaving the tuning rows in place.
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = N'EnableSessionTrendGate';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   FULL ROLLBACK — remove every row this script created.
   ---------------------------------------------------------------------------
DELETE FROM dbo.StrategySettings
 WHERE SettingKey = N'EnableSessionTrendGate' OR SettingKey LIKE N'SessionTrend%';
   --------------------------------------------------------------------------- */
