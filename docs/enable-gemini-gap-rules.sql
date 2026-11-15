/* ============================================================================
   Gemini gap rules — enable / tune / kill switch                     2026-10-21
   ----------------------------------------------------------------------------
   Every rule ships OFF. Nothing happens until a flag is set to 1.
   Claude does not execute this file (.claude/rules/02-approval-gate.md).

   WHAT IT ADDS
   ------------
   Five opening-range rules, evaluated by the single-signal rule fallback in
   StrategyManager only after the CHAIN produced no signal and the approved
   BANKNIFTY PUT BREAKDOWN rule did not match. Code:
   src/Trading.SignalEngine/Strategies/GeminiGapRuleEvaluator.cs

     EnableGeminiNormalOrb            no gap: break of the opening range + 5m RSI band + candle
     EnableGeminiGapUpGo              gap up:   above OR high + PCR >= min + bullish candle   (CALL)
     EnableGeminiGapUpFade            gap up:   below OR low AND (5m RSI >= min OR bearish candle) (PUT)
     EnableGeminiGapDownContinuation  gap down: below OR low + bearish candle                  (PUT)
     EnableGeminiGapDownRebound       gap down: above OR high + 5m RSI <= max + bullish candle (CALL)

   "Delta" in the source design is the entry-candle direction (ConfirmEntryCandle incl.
   BodyPctMinimum): the platform has no signed volume.

   *** READ THIS BEFORE ENABLING ***
   UNVALIDATED. No backtest backs these thresholds. Stored research found this
   family of gap rules at or below break-even after costs
   (docs/research/2026-10-15-gapup-proposal, 2026-10-16-session-trend), and the
   gap-down rebound CALL almost never fires. Enable on PAPER only, ONE rule and
   ONE symbol at a time, and read the RuleId in StrategyExecutionLog.Reason.

   SAFETY
   ------
   - Idempotent upsert; re-running changes nothing. All flags default to 0 here.
   - Touches only Gemini* / EnableGemini* rows.
   - These rules do NOT bypass any gate: EarlyTrendGate, SessionTrendGate, OI,
     duplicate, risk, R:R and spread gates all inspect the built Signal.
   - Settings cache refreshes ~5 minutes; no restart needed.
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @Symbol  nvarchar(50)  = NULL;   -- NULL = global, or 'BANKNIFTY' etc. (turn ON per symbol)

-- Flags: 1 = ON.  Leave at 0 until you have decided.
DECLARE @Orb     decimal(18,6) = 0;
DECLARE @GuGo    decimal(18,6) = 0;
DECLARE @GuFade  decimal(18,6) = 0;
DECLARE @GdCont  decimal(18,6) = 0;
DECLARE @GdReb   decimal(18,6) = 0;

-- Tunables (source-design defaults)
DECLARE @OrMin   decimal(18,6) = 15;     -- 1m bars that form the opening range
DECLARE @MaxMin  decimal(18,6) = 120;    -- last entry, minutes after open (opening bars leave the 200-bar window ~200 min in)
DECLARE @BullFlr decimal(18,6) = 55;     -- normal open CALL: 5m RSI floor
DECLARE @BearCei decimal(18,6) = 45;     -- normal open PUT : 5m RSI ceiling
DECLARE @PcrMin  decimal(18,6) = 1.1;    -- gap-up go CALL: minimum PCR
DECLARE @FadeRsi decimal(18,6) = 75;     -- gap-up fade PUT: 5m RSI at/above
DECLARE @RebRsi  decimal(18,6) = 35;     -- gap-down rebound CALL: 5m RSI at/below

DECLARE @rows TABLE (SettingKey nvarchar(100), SettingValue decimal(18,6), Descr nvarchar(400));
INSERT INTO @rows VALUES
 (N'EnableGeminiNormalOrb',           @Orb,     N'Gemini normal-open ORB rule on/off. See docs/enable-gemini-gap-rules.sql'),
 (N'EnableGeminiGapUpGo',             @GuGo,    N'Gemini gap-up go CALL on/off'),
 (N'EnableGeminiGapUpFade',           @GuFade,  N'Gemini gap-up fade PUT on/off'),
 (N'EnableGeminiGapDownContinuation', @GdCont,  N'Gemini gap-down continuation PUT on/off'),
 (N'EnableGeminiGapDownRebound',      @GdReb,   N'Gemini gap-down rebound CALL on/off'),
 (N'GeminiOrMinutes',                 @OrMin,   N'Gemini: 1m bars forming the opening range'),
 (N'GeminiMaxMinutesFromOpen',        @MaxMin,  N'Gemini: last entry, minutes after the open'),
 (N'GeminiRsiBullFloor',              @BullFlr, N'Gemini normal open CALL: minimum 5m RSI'),
 (N'GeminiRsiBearCeiling',            @BearCei, N'Gemini normal open PUT: maximum 5m RSI'),
 (N'GeminiGapUpPcrMin',               @PcrMin,  N'Gemini gap-up go CALL: minimum PCR'),
 (N'GeminiFadeRsiMin',                @FadeRsi, N'Gemini gap-up fade PUT: 5m RSI at or above (OR bearish candle)'),
 (N'GeminiReboundRsiMax',             @RebRsi,  N'Gemini gap-down rebound CALL: 5m RSI at or below');

MERGE dbo.StrategySettings AS t
USING @rows AS s
   ON t.SettingKey = s.SettingKey
  AND ISNULL(t.Symbol, N'~') = ISNULL(@Symbol, N'~')
WHEN MATCHED THEN
    UPDATE SET SettingValue = s.SettingValue, IsActive = 1, UpdatedOn = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (s.SettingKey, @Symbol, s.SettingValue, 1, s.Descr, SYSDATETIME());

PRINT CONCAT('Gemini gap rule rows upserted: ', @@ROWCOUNT);
GO

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey LIKE N'EnableGemini%' OR SettingKey LIKE N'Gemini%'
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   KILL SWITCH — turn every rule off, leaving the tuning rows in place.
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey LIKE N'EnableGemini%';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   FULL ROLLBACK — remove every row this script created.
   ---------------------------------------------------------------------------
DELETE FROM dbo.StrategySettings
 WHERE SettingKey LIKE N'EnableGemini%' OR SettingKey LIKE N'Gemini%';
   --------------------------------------------------------------------------- */
