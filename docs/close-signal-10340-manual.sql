-- Close paper signal 10340 (NIFTY26N1725350CE, CALL, 4 lots x 65, entry 249.54) - TradingDB_Paper only.
-- State found 2026-11-11: Status ACTIVE, RemainingQty 2, PartialPnL 2,120.30. Journal 30426 = PARTIAL_T1, 2 lots @ 265.85
-- (order 10953, COMPLETE). The last 2 lots were sold by hand on the Groww app at 276.00, so no SELL order exists in Orders.
-- Fix: close the signal and book the final 2 lots: (276.00 - 249.54) * 2 * 65 = 3,439.80.  Trade total = 2,120.30 + 3,439.80 = 5,560.10.
-- Run BrokerPositionSyncWorker-aware: the guards below only touch rows still in the open state, so if the worker has already
-- closed the signal / booked the exit, every statement affects 0 rows and nothing is double-booked. Check the first SELECT.
-- Claude did not execute this script.

DECLARE @SignalId  bigint        = 10340;
DECLARE @ExitPrice decimal(18,2) = 276.00;
DECLARE @Lots      int           = 2;
DECLARE @LotSize   int           = 65;                 -- 2,120.30 / (265.85 - 249.54) / 2 = 65
DECLARE @ExitTime  datetime2     = '2026-11-11 09:50:00';  -- time sold on Groww, IST (same clock as Signals.ExitTime)

IF @ExitTime IS NULL THROW 50001, 'Set @ExitTime (IST) before running.', 1;

-- PRE-CHECK: expect Status ACTIVE, RemainingQty 2, and no non-partial journal row yet.
SELECT Id, Status, RemainingQty, PartialPnL, ExitReason, ExitTime FROM Signals WHERE Id = @SignalId;
SELECT Id, Quantity, ExitPrice, ProfitLoss, IsPartial, ExitReason FROM TradeJournal WHERE SignalId = @SignalId;

SET XACT_ABORT ON;
BEGIN TRAN;

-- 1) Final journal row, context columns copied from the partial row.
INSERT INTO TradeJournal
    (Symbol, TradeType, EntryPrice, ExitPrice, Quantity, ProfitLoss, TradeDate, Remarks, SignalId, EntryOrderId, ExitOrderId,
     IsPartial, StrategyType, ExitReason, SignalConfidence, RsiAtEntry, Rsi5mAtEntry, PcrAtEntry, PcrFullChainAtEntry,
     DeltaAtEntry, ChangeInOiAtEntry, SelectedStrategy, MarketState, IsExpiryDay, EntryHour)
SELECT j.Symbol, j.TradeType, j.EntryPrice, @ExitPrice, @Lots,
       CAST((@ExitPrice - j.EntryPrice) * @Lots * @LotSize AS decimal(18,2)), @ExitTime,
       N'Manual close on Groww app', j.SignalId, j.EntryOrderId, NULL,
       0, j.StrategyType, N'MANUAL_CLOSE', j.SignalConfidence, j.RsiAtEntry, j.Rsi5mAtEntry, j.PcrAtEntry, j.PcrFullChainAtEntry,
       j.DeltaAtEntry, j.ChangeInOiAtEntry, j.SelectedStrategy, j.MarketState, j.IsExpiryDay, j.EntryHour
FROM TradeJournal j
WHERE j.Id = 30426 AND j.SignalId = @SignalId AND j.IsPartial = 1
  AND NOT EXISTS (SELECT 1 FROM TradeJournal x WHERE x.SignalId = @SignalId AND x.IsPartial = 0);
SELECT @@ROWCOUNT AS JournalRowsInserted;      -- expect 1

-- 2) Close the signal (PartialPnL keeps the T1 partial; same field the other exits accumulate into).
UPDATE Signals
SET    Status = 'CLOSED', RemainingQty = 0, ExitReason = 'MANUAL_CLOSE', ExitTime = @ExitTime
WHERE  Id = @SignalId AND Status = 'ACTIVE' AND RemainingQty = @Lots;
SELECT @@ROWCOUNT AS SignalsClosed;            -- expect 1

-- 3) Position row, if one is still open.
UPDATE Positions
SET    Status = 'CLOSED', ClosedAt = @ExitTime
WHERE  SignalId = @SignalId AND Status = 'OPEN';
SELECT @@ROWCOUNT AS PositionsClosed;          -- expect 0 or 1 (0 = already closed by the sync worker)

COMMIT;

-- VERIFY: signal CLOSED / RemainingQty 0; journal 2 lots @ 2,120.30 + 2 lots @ 3,439.80.
SELECT Id, Status, ExitReason, Quantity, RemainingQty, PartialPnL, ExitTime FROM Signals WHERE Id = @SignalId;
SELECT Id, Quantity, EntryPrice, ExitPrice, ProfitLoss, IsPartial, ExitReason FROM TradeJournal WHERE SignalId = @SignalId;
SELECT SUM(ProfitLoss) AS TradeTotal FROM TradeJournal WHERE SignalId = @SignalId;   -- expect 5,560.10
SELECT Id, Status, Quantity, AvgPrice, ClosedAt FROM Positions WHERE SignalId = @SignalId;

-- ROLLBACK (restores the state found on 2026-11-11):
-- DELETE FROM TradeJournal WHERE SignalId = 10340 AND IsPartial = 0 AND ExitReason = 'MANUAL_CLOSE';
-- UPDATE Signals   SET Status = 'ACTIVE', RemainingQty = 2, ExitReason = NULL, ExitTime = NULL WHERE Id = 10340;
-- UPDATE Positions SET Status = 'OPEN', ClosedAt = NULL WHERE SignalId = 10340;   -- only if step 3 changed a row
