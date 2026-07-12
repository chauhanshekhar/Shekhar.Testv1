namespace Trading.Domain;

public static class SignalStatus
{
    public const string Pending             = "PENDING";
    public const string OrderPlaced         = "ORDER_PLACED";
    public const string OrderRejected       = "ORDER_REJECTED";
    public const string OrderUnknown        = "ORDER_UNKNOWN";
    public const string Active              = "ACTIVE";
    public const string Closed              = "CLOSED";
    public const string CloseFailed         = "CLOSE_FAILED";
    public const string ManualCheckRequired = "MANUAL_CHECK_REQUIRED";
    public const string ExitPending = "EXIT_PENDING";
    public const string PartialExitPending = "PARTIAL_EXIT_PENDING";
}
