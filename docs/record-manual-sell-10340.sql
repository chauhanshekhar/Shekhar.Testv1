-- Record the Groww-app SELL of signal 10340's last 2 lots (130 units @ 276.00) so OnePositionGate sees the position as flat.
-- Why: the gate (OnePositionGate.CheckOrderExposureAsync) sums FilledQty of BUY entries vs SELL orders per signal for
-- OnePositionOrderHoldMinutes (default 15) after the last order. 10340 shows bought 260, sold 130 because a manual Groww
-- close leaves no SELL row, so every symbol stays suppressed until ~10:09:43 (last order 09:54:43).
-- This adds a COMPLETE SELL row (EXIT purpose, 130 @ 276.00) -> sold 260 = bought 260. Non-PAPER id on purpose (the gate ignores PAPER- rows).
-- Run AFTER docs/close-signal-10340-manual.sql. Claude did not execute this script. TradingDB_Paper.

DECLARE @SignalId bigint = 10340;
DECLARE @ExitTime datetime2 = '2026-11-11 09:50:00';   -- IST, same as the close script

-- PRE-CHECK: expect bought 260 / sold 130.
SELECT SUM(CASE WHEN Side='BUY'  AND OrderPurpose='ENTRY' THEN FilledQty ELSE 0 END) AS Bought,
       SUM(CASE WHEN Side='SELL' THEN FilledQty ELSE 0 END)                           AS Sold
FROM Orders WHERE SignalId = @SignalId;

IF NOT EXISTS (SELECT 1 FROM Orders WHERE SignalId = @SignalId AND BrokerOrderId = 'MANUAL-GROWW-APP-10340')
INSERT INTO Orders
    (SignalId, OrderReferenceId, Broker, BrokerOrderId, Symbol, Exchange, Side, OrderType, OrderPurpose, ProductType,
     Status, StatusMessage, Price, TriggerPrice, Quantity, FilledQty, AvgPrice, OrderedAt, UpdatedAt, RawPostback)
SELECT e.SignalId, 'MANUAL10340', 'GROWW', 'MANUAL-GROWW-APP-10340', e.Symbol, e.Exchange, 'SELL', 'MARKET', 'EXIT', e.ProductType,
       'COMPLETE', N'Closed manually on Groww app (recorded by script)', 276.00, 0, 130, 130, 276.00, @ExitTime, @ExitTime, ''
FROM Orders e
WHERE e.Id = 10949 AND e.SignalId = @SignalId;
SELECT @@ROWCOUNT AS RowsInserted;   -- expect 1 (0 = already recorded or order 10949 not found)

-- VERIFY: Bought 260, Sold 260.
SELECT SUM(CASE WHEN Side='BUY'  AND OrderPurpose='ENTRY' THEN FilledQty ELSE 0 END) AS Bought,
       SUM(CASE WHEN Side='SELL' THEN FilledQty ELSE 0 END)                           AS Sold
FROM Orders WHERE SignalId = @SignalId;

-- ROLLBACK:
-- DELETE FROM Orders WHERE SignalId = 10340 AND BrokerOrderId = 'MANUAL-GROWW-APP-10340';
