using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Regression cover for the 2026-08-24 BANKNIFTY incident (signal 18).
///
/// A LIMIT BUY took ~24 seconds to come back from Groww. While the signal sat PENDING,
/// BrokerPositionSyncWorker's 30-second cycle saw it missing from GetPositionsAsync — which lags
/// a fill anyway — and declared ORDER_REJECTED. The broker had in fact accepted and filled it.
///
/// ORDER_REJECTED is not in ExitDataLoader's open-signal set (OrderPlaced/Active/
/// PartialExitPending), so the live position dropped out of exit management entirely: the SL_M
/// failed once, was never retried, the stop level was hit, and the position had to be closed by
/// hand.
///
/// The guard's job is to never declare rejection while the order might still be alive.
/// </summary>
public class PendingRejectionGuardTests
{
    private static readonly DateTime Now = new(2026, 8, 24, 13, 10, 0, DateTimeKind.Unspecified);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    private static bool CanReject(
        DateTime createdOn,
        string? signalBrokerOrderId = "",
        IReadOnlyList<Order>? entryOrders = null) =>
        BrokerPositionSyncWorker.CanDeclarePendingRejected(
            createdOn, signalBrokerOrderId, entryOrders, Now, Grace, out _);

    private static string Reason(
        DateTime createdOn,
        string? signalBrokerOrderId = "",
        IReadOnlyList<Order>? entryOrders = null)
    {
        BrokerPositionSyncWorker.CanDeclarePendingRejected(
            createdOn, signalBrokerOrderId, entryOrders, Now, Grace, out var reason);
        return reason;
    }

    // ── The incident itself ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TheIncident_SignalOnlySecondsOld_IsNotRejected()
    {
        // Signal 18: created 13:06:02, judged while the placement was still in flight.
        var createdOn = Now.AddSeconds(-24);

        Assert.False(CanReject(createdOn));
        Assert.Contains("placement grace", Reason(createdOn));
    }

    [Fact]
    public void TheIncident_OnceGrowwReturnedAnOrderId_IsNeverRejected()
    {
        // Even long past the grace period: a broker order id means the broker accepted it.
        var createdOn = Now.AddHours(-2);

        Assert.False(CanReject(createdOn, signalBrokerOrderId: "GLTFO260824130626J9SDEIRM5JXL"));
        Assert.Contains("BrokerOrderId", Reason(createdOn, signalBrokerOrderId: "GLTFO260824130626J9SDEIRM5JXL"));
    }

    // ── Deference to BrokerOrderSyncWorker, the source of truth ─────────────────────────────

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.OrderUnknown)]
    public void EntryOrderStillReconciling_IsNotRejected(string status)
    {
        var orders = new[] { new Order { Id = 7, Status = status, BrokerOrderId = "" } };

        Assert.False(CanReject(Now.AddHours(-2), entryOrders: orders));
        Assert.Contains("reconciling", Reason(Now.AddHours(-2), entryOrders: orders));
    }

    [Fact]
    public void EntryOrderCarryingABrokerId_IsNotRejected()
    {
        var orders = new[] { new Order { Id = 7, Status = OrderStatus.Failed, BrokerOrderId = "GLTFO-XYZ" } };

        Assert.False(CanReject(Now.AddHours(-2), entryOrders: orders));
        Assert.Contains("BrokerOrderId", Reason(Now.AddHours(-2), entryOrders: orders));
    }

    // ── The genuine case the branch exists for ──────────────────────────────────────────────

    [Fact]
    public void OrderThatTrulyNeverReachedTheBroker_IsStillRejected()
    {
        // Crash between saving the signal and calling PlaceOrderAsync: past grace, no broker id
        // anywhere, entry order terminally FAILED. This must still be cleaned up.
        var orders = new[] { new Order { Id = 7, Status = OrderStatus.Failed, BrokerOrderId = "" } };

        Assert.True(CanReject(Now.AddHours(-2), entryOrders: orders));
    }

    [Fact]
    public void OldSignalWithNoOrderRowAtAll_IsStillRejected()
    {
        Assert.True(CanReject(Now.AddHours(-2), entryOrders: null));
    }

    [Fact]
    public void ExactlyAtTheGraceBoundary_IsEligible()
    {
        Assert.False(CanReject(Now.AddMinutes(-5).AddSeconds(1)));   // just inside
        Assert.True(CanReject(Now.AddMinutes(-5)));                   // exactly at
    }
}
