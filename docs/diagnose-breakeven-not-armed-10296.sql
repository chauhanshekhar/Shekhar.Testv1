-- READ-ONLY diagnostic: why did signal 10296 (BANKNIFTY PUT, 2026-10-27) not arm breakeven?
-- Hypothesis: premium peaked at ~129.20, entry 114.30 + BreakevenBufferPoints (default 15) = 129.30.
-- Writes nothing. SELECT only. Does not touch SystemSettings.
-- Column names verified against src/Trading.Domain/Entities (Signal, MarketTick, OptionMinuteHistory, TrailingStopLog).

DECLARE @SignalId bigint = 10296;
-- MarketTicks/OptionMinuteHistory time zone is not confirmed (candle times are UTC; Signals are IST).
-- If sections 4/5 return no rows, set @TickOffsetHours = -5.5 (tick time stored in UTC) and re-run.
DECLARE @TickOffsetHours decimal(4,1) = 0;

-- 1. The signal: entry, stop, targets, excursions, protection state
SELECT s.Id, s.Symbol, s.OptionSymbol, s.CreatedOn, s.ExitTime, s.ExitReason, s.Quantity, s.RemainingQty, s.Status,
       s.OptionLtp AS EntryPremium, s.OptionSL, s.OptionT1, s.OptionT2,
       s.MfePrice, s.MaePrice,
       s.MfePrice - s.OptionLtp          AS MfePointsAboveEntry,
       s.OptionT1 - s.MfePrice           AS PointsShortOfT1,
       s.ProtectedSL, s.IndexT1Hit,
       s.StopLossStatus, s.StopLossTriggerPrice, s.LastSlUpdateTime
FROM Signals s WITH (NOLOCK)
WHERE s.Id = @SignalId;

-- 2. Breakeven settings in force (per-symbol row wins over global; IsActive=0 rows are ignored by the service)
SELECT SettingKey, ISNULL(Symbol, '(global)') AS Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WITH (NOLOCK)
WHERE SettingKey IN ('EnableBreakevenArm', 'BreakevenBufferPoints', 'EnableBreakevenPartialSale',
                     'EnableStepLock', 'EnableTargetLadder', 'TrailSLActivationPct')
  AND (Symbol IS NULL OR Symbol = 'BANKNIFTY')
ORDER BY SettingKey, Symbol;

-- 3. Computed arm level vs. recorded peak, using the effective buffer (symbol row, else global, else compiled 15)
SELECT s.OptionLtp AS EntryPremium,
       b.Buffer,
       s.OptionLtp + b.Buffer              AS ArmLevel,
       s.MfePrice                          AS RecordedPeak,
       (s.OptionLtp + b.Buffer) - s.MfePrice AS ShortOfArm   -- > 0 means arm level was never reached
FROM Signals s WITH (NOLOCK)
CROSS APPLY (
    SELECT COALESCE(
        (SELECT TOP (1) SettingValue FROM StrategySettings WITH (NOLOCK)
          WHERE SettingKey = 'BreakevenBufferPoints' AND Symbol = s.Symbol AND IsActive = 1),
        (SELECT TOP (1) SettingValue FROM StrategySettings WITH (NOLOCK)
          WHERE SettingKey = 'BreakevenBufferPoints' AND Symbol IS NULL AND IsActive = 1),
        15) AS Buffer
) b
WHERE s.Id = @SignalId;

-- 4. Option premium path from entry to exit, per-minute high/low/last (MarketTicks). The real peak
--    can exceed Signals.MfePrice, which is only updated once per ExitWorker cycle.
SELECT DATEADD(MINUTE, DATEDIFF(MINUTE, 0, t.TickTime), 0) AS TickMinute,
       MIN(t.LastPrice) AS LowLtp, MAX(t.LastPrice) AS HighLtp, COUNT(*) AS Ticks
FROM Signals s WITH (NOLOCK)
JOIN MarketTicks t WITH (NOLOCK)
  ON t.Symbol = s.OptionSymbol
 AND t.TickTime >= DATEADD(MINUTE, DATEDIFF(MINUTE, 0, DATEADD(MINUTE, @TickOffsetHours * -60, s.CreatedOn)), 0)
 AND t.TickTime <= DATEADD(MINUTE, @TickOffsetHours * -60, ISNULL(s.ExitTime, SYSDATETIME()))
WHERE s.Id = @SignalId
GROUP BY DATEADD(MINUTE, DATEDIFF(MINUTE, 0, t.TickTime), 0)
ORDER BY TickMinute;

-- 4b. Single highest tick in the window (the true peak premium)
SELECT TOP (1) t.TickTime, t.LastPrice AS PeakTickLtp
FROM Signals s WITH (NOLOCK)
JOIN MarketTicks t WITH (NOLOCK)
  ON t.Symbol = s.OptionSymbol
 AND t.TickTime >= DATEADD(MINUTE, @TickOffsetHours * -60, s.CreatedOn)
 AND t.TickTime <= DATEADD(MINUTE, @TickOffsetHours * -60, ISNULL(s.ExitTime, SYSDATETIME()))
WHERE s.Id = @SignalId
ORDER BY t.LastPrice DESC;

-- 5. Minute-bar premium history, if it exists (OptionMinuteHistory was truncated on 2026-10-07; may be empty)
SELECT h.MinuteTime, h.Ltp, h.UnderlyingLtp
FROM Signals s WITH (NOLOCK)
JOIN OptionMinuteHistory h WITH (NOLOCK)
  ON h.TradingSymbol = s.OptionSymbol
 AND h.MinuteTime >= DATEADD(MINUTE, @TickOffsetHours * -60, s.CreatedOn)
 AND h.MinuteTime <= DATEADD(MINUTE, @TickOffsetHours * -60, ISNULL(s.ExitTime, SYSDATETIME()))
WHERE s.Id = @SignalId
ORDER BY h.MinuteTime;

-- 6. Stop-trailing activity for this signal (confirms whether any stop move happened)
SELECT CreatedOn, OldOptionSL, NewOptionSL, MfePrice
FROM TrailingStopLogs WITH (NOLOCK)
WHERE SignalId = @SignalId
ORDER BY CreatedOn;

-- Reading the result:
--   Section 3 ShortOfArm > 0  and  section 4b peak < ArmLevel  -> arm level never reached; working as designed.
--   Section 4b peak >= ArmLevel but ProtectedSL = 0 in section 1 -> the arm was missed (ExitWorker cycle gap / tick not seen): a real defect to investigate.
--   Section 2 shows EnableBreakevenArm = 0 for BANKNIFTY      -> the feature was switched off.
