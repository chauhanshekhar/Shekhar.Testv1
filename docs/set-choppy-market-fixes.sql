/* =====================================================================================================================
   set-choppy-market-fixes.sql
   Switches the choppy-market fixes on or off (StrategySettings). Every key below is read by live SignalWorker /
   StrategyManager: per-symbol row first, then the global row (Symbol IS NULL), and with NO row each fix is OFF.

     Fix 1  EnableCloseBreakoutGate    + BreakoutLookbackCandles  - entry only on a CLOSED 5m breakout of N bars
            Left OFF here: EnableBreakoutProtection (day high/low breakout + pullback + continuation) already
            covers breakout/breakdown entries. Run both only if you deliberately want both filters.
     Fix 2  EnableAtr5mStop            + Atr5mStopMultiplier      - index stop from 5m ATR instead of 1m ATR
            EnableStopNoiseGate        + StopNoiseMinRatio, StopNoiseBars - block stops smaller than 5m noise
     Fix 3  EnableEfficiencyRatioGate  + MinEfficiencyRatio, EfficiencyRatioBars - block 5m chop
     Fix 4  EnableMaxDailyTradesGate   + MaxTradesPerDay          - cap new signals per day (all symbols)
     Fix 5  Target2RMultiple           - T2 placed at N x R (default geometry is 1.3R). Rollback = IsActive 0
     Fix 6  EnableTrend5mGate          + Trend5mGateSide, Trend5mRequirePriceSide - CALL/PUT only with the 5m EMA20/EMA50
     Fix 7  EnableOpeningWarmupGate    + OpeningWarmupEndMinute   - no entries 09:15-09:45 while 1m EMAs warm up
   MaxConsecutiveLosses is NOT touched (stays as configured).

   These new gates are NOT replayed by Trading.Backtest. Paper-trade first.
   NOT executed by Claude - review, then run it yourself.
   ===================================================================================================================== */
SET XACT_ABORT ON;

DECLARE @On      decimal(18, 6) = 1;      -- 1 = switch the fixes on, 0 = off
DECLARE @Symbol  nvarchar(50)   = NULL;   -- NULL = global rows; or 'NIFTY' / 'BANKNIFTY' / 'SENSEX'
DECLARE @Now     datetime2      = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IST, as IstClock.Now

DECLARE @Rows TABLE (SettingKey nvarchar(100), SettingValue decimal(18, 6), Description nvarchar(400));
INSERT INTO @Rows VALUES
 ('EnableCloseBreakoutGate',   0,     'Fix 1: CALL/PUT only when the last closed 5m bar closed beyond the N-bar high/low and LTP is still beyond it. 1 = on.'),
 ('BreakoutLookbackCandles',   5,     'Fix 1: number of previous closed 5m bars the breakout close must clear.'),
 ('EnableAtr5mStop',           @On,   'Fix 2: index stop = Atr5mStopMultiplier x ATR(14) on closed 5m bars (clamped as before). 0 = 1m ATR as before.'),
 ('Atr5mStopMultiplier',       1.0,   'Fix 2: multiple of 5m ATR used for the index stop.'),
 ('EnableStopNoiseGate',       @On,   'Fix 2 guard: block when index stop < StopNoiseMinRatio x average 5m range. 1 = on.'),
 ('StopNoiseMinRatio',         1.0,   'Fix 2 guard: required stop as a multiple of the average 5m bar range.'),
 ('StopNoiseBars',             6,     'Fix 2 guard: closed 5m bars averaged for the noise range.'),
 ('EnableEfficiencyRatioGate', @On,   'Fix 3: block entries when 5m Efficiency Ratio < MinEfficiencyRatio (choppy). 1 = on.'),
 ('MinEfficiencyRatio',        0.30,  'Fix 3: minimum 5m Efficiency Ratio (net move / path). 0 = chop, 1 = straight line.'),
 ('EfficiencyRatioBars',       6,     'Fix 3: closed 5m bars in the Efficiency Ratio window (6 = 30 minutes).'),
 ('EnableMaxDailyTradesGate',  @On,   'Fix 4: block new signals once today''s signal count (all symbols, excl. rejected) reaches MaxTradesPerDay.'),
 ('MaxTradesPerDay',           6,     'Fix 4: max signals per day across all symbols. 0 = unlimited.'),
 ('Target2RMultiple',          2.0,   'Fix 5: Target2 (index and option) at this many R from entry. Must exceed the T1 multiple (1R) or it is ignored.'),
 ('EnableTrend5mGate',         @On,   'Fix 6: CALL needs 5m EMA20 > EMA50, PUT needs below. Missing 5m EMAs block. 1 = on.'),
 ('Trend5mGateSide',           0,     'Fix 6: 0 = CALL and PUT, 1 = CALL only, 2 = PUT only.'),
 ('Trend5mRequirePriceSide',   0,     'Fix 6: 1 = also require LTP beyond the 5m EMA20.'),
 ('EnableOpeningWarmupGate',   0,     'Fix 7: no new entries from 09:15 until OpeningWarmupEndMinute. Left OFF: it also skips the morning move. 1 = on.'),
 ('OpeningWarmupEndMinute',    585,   'Fix 7: minutes after IST midnight, exclusive (585 = 09:45).');

BEGIN TRANSACTION;

UPDATE s
   SET s.SettingValue = r.SettingValue, s.IsActive = 1, s.UpdatedOn = @Now
  FROM StrategySettings s
  JOIN @Rows r ON r.SettingKey = s.SettingKey
 WHERE (@Symbol IS NULL AND s.Symbol IS NULL) OR s.Symbol = @Symbol;

INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
SELECT r.SettingKey, @Symbol, r.SettingValue, 1, r.Description, @Now
  FROM @Rows r
 WHERE NOT EXISTS (SELECT 1 FROM StrategySettings s
                   WHERE s.SettingKey = r.SettingKey
                     AND ((@Symbol IS NULL AND s.Symbol IS NULL) OR s.Symbol = @Symbol));

COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey IN ('EnableCloseBreakoutGate','BreakoutLookbackCandles','EnableAtr5mStop','Atr5mStopMultiplier',
                     'EnableStopNoiseGate','StopNoiseMinRatio','StopNoiseBars','EnableEfficiencyRatioGate',
                     'MinEfficiencyRatio','EfficiencyRatioBars','EnableMaxDailyTradesGate','MaxTradesPerDay',
                     'Target2RMultiple','EnableTrend5mGate','Trend5mGateSide','Trend5mRequirePriceSide',
                     'EnableOpeningWarmupGate','OpeningWarmupEndMinute')
ORDER BY SettingKey, Symbol;
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK (switch every fix off; no data is deleted) -----------------------------------------------------------
UPDATE StrategySettings SET SettingValue = 0, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
WHERE SettingKey IN ('EnableCloseBreakoutGate','EnableAtr5mStop','EnableStopNoiseGate',
                     'EnableEfficiencyRatioGate','EnableMaxDailyTradesGate',
                     'EnableTrend5mGate','EnableOpeningWarmupGate');
-- Target2RMultiple is a value, not a switch: deactivate the row to return to the default 1.3R.
UPDATE StrategySettings SET IsActive = 0, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
WHERE SettingKey = 'Target2RMultiple';
------------------------------------------------------------------------------------------------------------------------ */
