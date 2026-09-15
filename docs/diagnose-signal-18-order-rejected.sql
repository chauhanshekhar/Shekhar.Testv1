/*
================================================================================
Confirm the signal-18 post-mortem  (2026-08-24 BANKNIFTY PUT)
================================================================================
READ-ONLY. SELECT only.

Hypothesis: BrokerPositionSyncWorker declared the signal ORDER_REJECTED while the
entry order was still in flight at Groww. The broker then filled it, but
ORDER_REJECTED is not in ExitDataLoader's open-signal set, so the live position
was never managed.

Run all four. Query 2 is the decisive one.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @SignalId bigint = 18;


/* ── 1. The signal ────────────────────────────────────────────────────────────
   Look for the contradiction: Status = ORDER_REJECTED while BrokerOrderStatus
   = COMPLETE. Only BrokerPositionSyncWorker can produce that pairing — every
   other rejection path overwrites BrokerOrderStatus at the same time.
   ---------------------------------------------------------------------------- */
SELECT Id, Symbol, SignalType, Status, BrokerOrderStatus, BrokerOrderId,
       OrderReferenceId, CreatedOn, ExitTime, ExitReason,
       OptionSymbol, OptionLtp, OptionSL,
       StopLossOrderId, StopLossStatus, StopLossTriggerPrice, StopLossRetryCount,
       OrderSyncAttempts, LastOrderSyncTime
FROM   dbo.Signals
WHERE  Id = @SignalId;


/* ── 2. DECISIVE — every order for this signal, in time order ─────────────────
   Expect: an ENTRY order that reached COMPLETE at the broker (so the fill was
   real), and a STOPLOSS order that was rejected/failed exactly once.

   Compare Signals.CreatedOn against the ENTRY order's OrderedAt / UpdatedAt.
   A gap of more than a few seconds is the placement round-trip during which the
   signal sat PENDING and was wrongly judged.
   ---------------------------------------------------------------------------- */
SELECT Id, SignalId, OrderPurpose, Side, OrderType, Status, StatusMessage,
       BrokerOrderId, OrderReferenceId, Quantity, Price, TriggerPrice,
       FilledQty, AvgPrice, OrderedAt, UpdatedAt
FROM   dbo.Orders
WHERE  SignalId = @SignalId
ORDER  BY OrderedAt, Id;


/* ── 3. Was the position ever created, and did it stay open? ──────────────────
   "before close trade in tables i am getting open position exits" — this shows
   what the Positions row looked like while the trade was live.
   ---------------------------------------------------------------------------- */
SELECT *
FROM   dbo.Positions
WHERE  SignalId = @SignalId;


/* ── 4. What the app logged around the incident ───────────────────────────────
   The alert "Order rejected — ..." would indicate the placement path threw.
   Its ABSENCE, combined with a COMPLETE entry order, points at
   BrokerPositionSyncWorker instead.
   ---------------------------------------------------------------------------- */
SELECT TOP (100) Id, Source, Message, Symbol, Context, Severity, OccurredOn
FROM   dbo.AppErrors
WHERE  OccurredOn >= '2026-08-24 13:00:00'
  AND  OccurredOn <= '2026-08-24 13:30:00'
ORDER  BY OccurredOn;


/*
================================================================================
5. HOW MANY OTHER TRADES DID THIS HIT?
================================================================================
Any signal marked ORDER_REJECTED that nonetheless has a broker order id, or a
COMPLETE entry order, was a REAL position the system stopped managing. Each one
is money that was left unprotected.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT s.Id, s.Symbol, s.SignalType, s.CreatedOn, s.Status, s.BrokerOrderStatus,
       s.BrokerOrderId, s.OptionSymbol, s.StopLossStatus, s.ExitReason,
       o.Status AS EntryOrderStatus, o.BrokerOrderId AS EntryBrokerOrderId,
       o.FilledQty, o.AvgPrice
FROM        dbo.Signals AS s
LEFT JOIN   dbo.Orders  AS o
       ON   o.SignalId = s.Id
      AND   o.OrderPurpose = 'ENTRY'
WHERE  s.Status = 'ORDER_REJECTED'
  AND (
        s.BrokerOrderId <> ''
     OR s.BrokerOrderStatus = 'COMPLETE'
     OR o.Status = 'COMPLETE'
     OR o.BrokerOrderId <> ''
      )
ORDER  BY s.CreatedOn DESC;
