-- READ-ONLY diagnostic: why did a TRAILING_SL_HIT exit fill BELOW entry?
-- Pasted row: TradeJournal Id 30362, BANKNIFTY PUT, entry 109.80, exit 109.55, ProfitLoss -30,
-- ExitReason TRAILING_SL_HIT. The 10291 in that row is the SignalId, so the signal is resolved
-- from the journal row below (change @JournalId only).
-- Expectation to confirm: Signal.OptionSL > entry (label rule) while the SL_M fill (Orders.AvgPrice)
-- is < entry (market fill after the trigger = slippage). No writes; no SystemSettings access.

DECLARE @JournalId bigint = 30362;
DECLARE @SignalId  bigint = (SELECT SignalId FROM TradeJournal WHERE Id = @JournalId);

-- 1. Journal rows for the signal (partial T1 row + final exit row)
SELECT Id, SignalId, Symbol, TradeType, StrategyType, IsPartial, EntryPrice, ExitPrice, Quantity,
       ProfitLoss, ExitReason, EntryOrderId, ExitOrderId, TradeDate
FROM TradeJournal
WHERE SignalId = @SignalId
ORDER BY Id;

-- 2. Signal state at close: stops, T1/T2, excursion, resting broker stop
SELECT Id, Symbol, SignalType, Status, ExitReason, ExitTime,
       OptionLtp AS EntryPremium, OptionSL, ProtectedSL, OptionT1, OptionT2,
       MfePrice, MaePrice, IndexT1Hit, Quantity, RemainingQty, PartialPnL,
       StopLossOrderId, StopLossStatus, StopLossTriggerPrice, LastSlUpdateTime
FROM Signals
WHERE Id = @SignalId;

-- 3. Every ATR-trail move: did OptionSL climb above entry, and by how much?
SELECT t.Id, t.CreatedOn, t.OldOptionSL, t.NewOptionSL,
       t.NewOptionSL - s.OptionLtp AS NewSlVsEntry,
       t.TargetSL, t.MaxStep, t.OptionAtrDistance, t.AtrRaw, t.Delta, t.MfePrice
FROM TrailingStopLogs t
JOIN Signals s ON s.Id = t.SignalId
WHERE t.SignalId = @SignalId
ORDER BY t.CreatedOn, t.Id;

-- 4. All orders: entry, partial SELL, each resting SL_M (cancel/replace history), final fill
--    Compare TriggerPrice (stop level) with AvgPrice (actual fill) on the COMPLETE stop order.
SELECT Id, OrderedAt, UpdatedAt, Side, OrderType, OrderPurpose, Status, StatusMessage,
       Price, TriggerPrice, Quantity, FilledQty, AvgPrice,
       AvgPrice - TriggerPrice AS FillVsTrigger,
       BrokerOrderId, OrderReferenceId
FROM Orders
WHERE SignalId = @SignalId
ORDER BY OrderedAt, Id;

-- 5. Fill vs trigger vs entry for the closing stop order (one-line answer)
SELECT TOP (1) o.Id AS StopOrderId, o.TriggerPrice, o.AvgPrice AS FillPrice,
       s.OptionLtp AS EntryPremium, s.OptionSL AS SignalOptionSL, s.ProtectedSL,
       CASE WHEN s.OptionSL > s.OptionLtp THEN 'label=TRAILING_SL_HIT' ELSE 'label=OPTION_SL_HIT' END AS LabelRule,
       CASE WHEN o.AvgPrice < s.OptionLtp THEN 'LOSS FILL (below entry)' ELSE 'profit/flat fill' END AS FillResult
FROM Orders o
JOIN Signals s ON s.Id = o.SignalId
WHERE o.SignalId = @SignalId
  AND o.OrderPurpose = 'STOPLOSS'
  AND o.Status IN ('COMPLETE', 'COMPLETED', 'EXECUTED')
ORDER BY o.UpdatedAt DESC;

-- 6. Stop-related feature flags in force for BANKNIFTY (specific keys only)
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey IN ('EnableBreakevenArm', 'BreakevenBufferPoints', 'EnableBreakevenPartialSale',
                     'EnableStepLock', 'EnableTargetLadder')
  AND (Symbol = 'BANKNIFTY' OR Symbol IS NULL)
ORDER BY SettingKey, Symbol;
