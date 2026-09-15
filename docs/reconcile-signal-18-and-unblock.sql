/*
================================================================================
Reconcile signal 18 and unblock OnePositionGate
================================================================================
This does NOT touch the broker. It only makes the database tell the truth about
a trade that was already closed by hand at Groww.

WHY EVERYTHING IS BLOCKED
OnePositionGate (SignalGates/OnePositionGate.cs) blocks like this:

    todaySignals = every Signal with CreatedOn >= today      -- NO symbol filter
    closedIds    = every TradeJournal today that has a SignalId
    block if     todaySignals.Any(s => !closedIds.Contains(s.Id))

So ANY signal created today that has no TradeJournal row suppresses new signals
for EVERY symbol for the rest of the day. Signal 18 is ORDER_REJECTED with no
journal row, which is why SENSEX, BANKNIFTY and NIFTY are all blocked.

Writing the missing TradeJournal row is therefore both the honest record of the
trade AND what releases the gate. Nothing is deleted.

BEFORE YOU RUN
  * Confirm at Groww that the position really is closed.
  * Get the ACTUAL exit fill price. Do not guess it — it drives ProfitLoss and
    feeds DailyPnL, CircuitBreakerGate and ConsecutiveLossGate.
  * Set @ExitPrice and @ExitTime below from the Groww order book.
================================================================================
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @SignalId  bigint   = 18;

-- >>> FILL THESE IN FROM THE GROWW ORDER BOOK <<<
DECLARE @ExitPrice decimal(18,2) = NULL;                          -- actual SELL fill price
DECLARE @ExitTime  datetime2     = NULL;                          -- actual exit time
DECLARE @ExitReason varchar(30)  = 'MANUAL_CLOSE';

IF @ExitPrice IS NULL OR @ExitTime IS NULL
BEGIN
    RAISERROR('Set @ExitPrice and @ExitTime from the Groww order book first. Refusing to guess a fill price.', 16, 1);
    RETURN;
END


/* ── 1. BEFORE ──────────────────────────────────────────────────────────────── */
SELECT 'SIGNAL BEFORE' AS Stage, Id, Symbol, SignalType, Status, BrokerOrderStatus,
       OptionSymbol, OptionLtp, Quantity, StrategyType, SelectedStrategy, MarketState,
       Confidence, RsiAtEntry, PcrAtEntry, DeltaAtEntry, ChangeInOiAtEntry,
       IsExpiryDay, CreatedOn, ExitTime, ExitReason
FROM   dbo.Signals WHERE Id = @SignalId;

SELECT 'JOURNAL BEFORE' AS Stage, * FROM dbo.TradeJournal WHERE SignalId = @SignalId;

-- Everything currently holding the gate shut (all symbols).
SELECT 'BLOCKING THE GATE' AS Stage, s.Id, s.Symbol, s.Status, s.CreatedOn
FROM        dbo.Signals AS s
LEFT JOIN   dbo.TradeJournal AS j
       ON   j.SignalId = s.Id AND j.TradeDate >= CAST(GETDATE() AS DATE)
WHERE  s.CreatedOn >= CAST(GETDATE() AS DATE)
  AND  j.Id IS NULL
ORDER  BY s.Id;


/* ── 2. APPLY ───────────────────────────────────────────────────────────────── */
BEGIN TRANSACTION;

    -- 2a. The missing journal row. Lot size is applied so ProfitLoss is in rupees,
    --     matching how TradeCompletionService computes it for normal exits.
    IF NOT EXISTS (SELECT 1 FROM dbo.TradeJournal WHERE SignalId = @SignalId AND IsPartial = 0)
    BEGIN
        INSERT INTO dbo.TradeJournal
            (Symbol, TradeType, EntryPrice, ExitPrice, Quantity, ProfitLoss, TradeDate, Remarks,
             SignalId, EntryOrderId, ExitOrderId, IsPartial, StrategyType, ExitReason,
             SignalConfidence, RsiAtEntry, PcrAtEntry, DeltaAtEntry, ChangeInOiAtEntry,
             SelectedStrategy, MarketState, IsExpiryDay, EntryHour)
        SELECT
            s.Symbol,
            s.SignalType,
            s.OptionLtp,                                   -- option entry premium
            @ExitPrice,
            s.Quantity,
            (@ExitPrice - s.OptionLtp) * s.Quantity
                * ISNULL((SELECT TOP (1) i.LotSize FROM dbo.TradingInstruments i
                          WHERE i.Symbol = s.Symbol), 1),
            @ExitTime,
            'MANUAL_RECONCILE_' + CAST(@SignalId AS varchar(20)),
            s.Id,
            (SELECT TOP (1) o.Id FROM dbo.Orders o
              WHERE o.SignalId = s.Id AND o.OrderPurpose = 'ENTRY' ORDER BY o.OrderedAt),
            NULL,                                          -- exit was placed by hand, not by the app
            0,
            s.StrategyType,
            @ExitReason,
            s.Confidence, s.RsiAtEntry, s.PcrAtEntry, s.DeltaAtEntry, s.ChangeInOiAtEntry,
            s.SelectedStrategy, s.MarketState, s.IsExpiryDay, DATEPART(HOUR, s.CreatedOn)
        FROM dbo.Signals AS s
        WHERE s.Id = @SignalId;
    END

    -- 2b. Make the signal say what actually happened. CLOSED, not ORDER_REJECTED:
    --     the order was never rejected, it filled and was closed manually.
    UPDATE dbo.Signals
    SET    Status         = 'CLOSED',
           ExitTime       = @ExitTime,
           ExitReason     = @ExitReason,
           StopLossStatus = '',
           StopLossOrderId = ''
    WHERE  Id = @SignalId;

    -- 2c. Close the Position row if one is still open.
    UPDATE dbo.Positions
    SET    Status = 'CLOSED'
    WHERE  SignalId = @SignalId
      AND  Status <> 'CLOSED';

COMMIT TRANSACTION;


/* ── 3. VERIFY ──────────────────────────────────────────────────────────────── */
SELECT 'SIGNAL AFTER' AS Stage, Id, Symbol, Status, ExitTime, ExitReason FROM dbo.Signals WHERE Id = @SignalId;
SELECT 'JOURNAL AFTER' AS Stage, Id, SignalId, Symbol, EntryPrice, ExitPrice, Quantity, ProfitLoss, ExitReason, Remarks
FROM   dbo.TradeJournal WHERE SignalId = @SignalId;
SELECT 'POSITION AFTER' AS Stage, * FROM dbo.Positions WHERE SignalId = @SignalId;

-- Must return ZERO rows, otherwise the gate is still shut.
SELECT 'STILL BLOCKING' AS Stage, s.Id, s.Symbol, s.Status, s.CreatedOn
FROM        dbo.Signals AS s
LEFT JOIN   dbo.TradeJournal AS j
       ON   j.SignalId = s.Id AND j.TradeDate >= CAST(GETDATE() AS DATE)
WHERE  s.CreatedOn >= CAST(GETDATE() AS DATE)
  AND  j.Id IS NULL
ORDER  BY s.Id;


/*
================================================================================
IF "STILL BLOCKING" RETURNS ROWS
================================================================================
Each remaining row is another signal from today with no journal entry. Check
each one at the broker before doing anything:

  * genuinely filled and still open   -> close it at Groww first, then reconcile
                                          it the same way as above
  * genuinely never placed            -> it needs a journal row too, or it keeps
                                          the gate shut all day

The gate counting never-placed signals as "open positions" is a design fault in
OnePositionGate, not something to paper over per-row every day. See the notes
returned with this script.
================================================================================
*/
