namespace Trading.Domain;

/// <summary>
/// Our own order-lifecycle states, stored in the same Order.Status column as Groww's
/// reported states (NEW/ACKED/TRIGGER_PENDING/APPROVED/EXECUTED/COMPLETED/REJECTED/CANCELLED).
/// </summary>
public static class OrderStatus
{
    public const string Pending             = "PENDING";
    public const string Complete            = "COMPLETE";
    public const string Failed              = "FAILED";
    public const string OrderUnknown        = "ORDER_UNKNOWN";
    public const string ManualCheckRequired = "MANUAL_CHECK_REQUIRED";
    public const string Cancelled           = "CANCELLED";
    public const string Rejected            = "REJECTED";

}
