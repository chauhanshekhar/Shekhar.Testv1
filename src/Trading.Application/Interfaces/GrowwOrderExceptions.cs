namespace Trading.Application.Interfaces;

/// <summary>Groww definitely rejected the order (a parsed 4xx response) — safe to mark Order.Status = FAILED.</summary>
public class GrowwOrderRejectedException : Exception
{
    public GrowwOrderRejectedException(string message) : base(message) { }
}

/// <summary>
/// Timeout/network/5xx while placing an order — Groww may have processed it anyway.
/// Must NOT be treated as a failed order; caller marks Order.Status = ORDER_UNKNOWN and
/// leaves reconciliation to BrokerOrderSyncWorker via OrderReferenceId.
/// </summary>
public class GrowwOrderAmbiguousException : Exception
{
    public GrowwOrderAmbiguousException(string message) : base(message) { }
}
