-- Repair paper signal 10316 (BANKNIFTY26NOV54800PE, 4 lots x 30, entry 698.25) — TradingDB_Paper only.
-- Cause: at 13:08:17 a PARTIAL_T1 SELL (order 10856, 2 lots @ 716.55) was pending when ExitWorker's paper path
-- booked a full PROTECTED_SL_HIT exit for all 4 lots (journal 30395, ExitOrderId 0) and closed the signal. 100 ms
-- later the partial fill was synced and CompletePartialExitAsync set Status back to ACTIVE.
-- Result: signal shows ACTIVE forever, and 6 lots are booked on a 4-lot trade.
-- Fix: close the signal, and re-book the protected-stop exit for the 2 lots that were really left
--      (709.75 - 698.25) * 2 * 30 = 690 instead of 1,380.  Trade total becomes 1,098 + 690 = 1,788.
-- Claude did not execute this script. Guarded: each statement only touches rows still in the bad state.

SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE Signals
SET    Status = 'CLOSED', RemainingQty = 0
WHERE  Id = 10316 AND Status = 'ACTIVE';
SELECT @@ROWCOUNT AS SignalsClosed;          -- expect 1

UPDATE TradeJournal
SET    Quantity = 2, ProfitLoss = 690
WHERE  Id = 30395 AND SignalId = 10316 AND IsPartial = 0 AND Quantity = 4 AND ProfitLoss = 1380;
SELECT @@ROWCOUNT AS JournalRowsFixed;       -- expect 1

UPDATE SignalOutcomes
SET    PnL = 690
WHERE  Id = 30290 AND SignalId = 10316 AND PnL = 1380;
SELECT @@ROWCOUNT AS OutcomesFixed;          -- expect 1

COMMIT;

-- VERIFY: signal CLOSED / RemainingQty 0; journal rows 2 lots @ 690 + 2 lots @ 1,098 (sum 1,788); outcome 690.
SELECT Id, Status, ExitReason, Quantity, RemainingQty, PartialPnL, ExitTime FROM Signals WHERE Id = 10316;
SELECT Id, Quantity, EntryPrice, ExitPrice, ProfitLoss, IsPartial, ExitReason, ExitOrderId FROM TradeJournal WHERE SignalId = 10316;
SELECT SUM(ProfitLoss) AS TradeTotal FROM TradeJournal WHERE SignalId = 10316;
SELECT Id, Result, PnL, ExitReason FROM SignalOutcomes WHERE SignalId = 10316;

-- ROLLBACK (restores the state found on 2026-11-02):
-- UPDATE Signals        SET Status = 'ACTIVE', RemainingQty = 2 WHERE Id = 10316;
-- UPDATE TradeJournal   SET Quantity = 4, ProfitLoss = 1380    WHERE Id = 30395;
-- UPDATE SignalOutcomes SET PnL = 1380                          WHERE Id = 30290;
