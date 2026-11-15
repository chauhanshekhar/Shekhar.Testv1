using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// A rejected EXIT must never strand or abandon a live position.
///
/// Live incident, 2026-09-15 signal 60 (NIFTY2691523150PE):
///   14:24:42  entry filled, 65 qty
///   14:25-14:48  twelve SL_M orders rejected/cancelled; retry cap engaged at 6 (working)
///   15:01:40  SL_M MARKET fallback fired after 810s unprotected (working)
///   15:02:10  order 394 — SELL / MARKET / OrderPurpose=STOPLOSS — REJECTED
///   -> signal stuck in EXIT_PENDING, ExitTime NULL, position 68 OPEN, broker qty 0
///
/// Two defects combined:
///   A. PositionCloseService.MapExitReasonToPurpose maps OptionSlHit -> OrderPurpose.StopLoss, so
///      the MARKET exit was tagged like a resting SL_M and routed into ApplyDeadStopLoss, which
///      never touches signal.Status. The signal could never leave EXIT_PENDING.
///   B. The generic rejection path sets ORDER_REJECTED, which is terminal — correct for an entry
///      that never opened, but for a rejected exit it drops a LIVE position out of
///      ExitDataLoader and releases OnePositionGate.
/// </summary>
public class ExitPendingDeadEndTests
{
    private static Order Sl_M() => new()
    {
        OrderPurpose = OrderPurpose.StopLoss, OrderType = "SL_M", Side = "SELL",
    };

    private static Order MarketExit(string purpose = OrderPurpose.StopLoss) => new()
    {
        OrderPurpose = purpose, OrderType = "MARKET", Side = "SELL",
    };

    private static Order Entry() => new()
    {
        OrderPurpose = OrderPurpose.Entry, OrderType = "MARKET", Side = "BUY",
    };

    // ── A. resting SL_M vs the MARKET sell that actually closes ──────────────

    [Fact]
    public void RestingSlM_IsTreatedAsAStopLoss()
    {
        Assert.True(BrokerOrderSyncWorker.IsRestingStopLoss(Sl_M()));
    }

    [Fact]
    public void Signal60_MarketFallbackExit_IsNotTreatedAsARestingStop()
    {
        // Order 394: SELL / MARKET / OrderPurpose=STOPLOSS. Purpose alone said "stop loss";
        // OrderType is what distinguishes the exit from the resting order.
        Assert.False(BrokerOrderSyncWorker.IsRestingStopLoss(MarketExit()));
    }

    [Theory]
    [InlineData(OrderPurpose.Exit)]
    [InlineData(OrderPurpose.Target)]
    [InlineData(OrderPurpose.ForceExit)]
    [InlineData(OrderPurpose.PartialExit)]
    public void OtherExitPurposes_AreNeverRestingStops(string purpose)
    {
        Assert.False(BrokerOrderSyncWorker.IsRestingStopLoss(MarketExit(purpose)));
    }

    [Fact]
    public void Entry_IsNeverARestingStop()
    {
        Assert.False(BrokerOrderSyncWorker.IsRestingStopLoss(Entry()));
    }

    // ── B. what status a rejection should produce ────────────────────────────

    [Fact]
    public void Signal60_RejectedExitWhileExitPending_ReturnsToActive()
    {
        // The position is live at the broker. ACTIVE keeps ExitWorker managing it so the exit
        // retries, instead of stranding it in EXIT_PENDING or abandoning it as ORDER_REJECTED.
        var signal = new Signal { Status = SignalStatus.ExitPending };

        Assert.Equal(
            SignalStatus.Active,
            BrokerOrderSyncWorker.ResolveRejectedOrderStatus(signal, MarketExit()));
    }

    [Theory]
    [InlineData(SignalStatus.ExitPending)]
    [InlineData(SignalStatus.PartialExitPending)]
    [InlineData(SignalStatus.Active)]
    public void RejectedExit_OnALivePosition_AlwaysReturnsToActive(string status)
    {
        var signal = new Signal { Status = status };

        Assert.Equal(
            SignalStatus.Active,
            BrokerOrderSyncWorker.ResolveRejectedOrderStatus(signal, MarketExit(OrderPurpose.Exit)));
    }

    [Fact]
    public void RejectedEntry_StillMarksOrderRejected()
    {
        // The entry never opened, so ORDER_REJECTED is correct — it is also what releases
        // OnePositionGate for the rest of the day.
        var signal = new Signal { Status = SignalStatus.OrderPlaced };

        Assert.Equal(
            SignalStatus.OrderRejected,
            BrokerOrderSyncWorker.ResolveRejectedOrderStatus(signal, Entry()));
    }

    [Fact]
    public void RejectedEntry_IsNotRescuedJustBecauseSignalLooksActive()
    {
        // A BUY/ENTRY rejection is terminal regardless of the signal's current status — only
        // exits are rescued.
        var signal = new Signal { Status = SignalStatus.Active };

        Assert.Equal(
            SignalStatus.OrderRejected,
            BrokerOrderSyncWorker.ResolveRejectedOrderStatus(signal, Entry()));
    }

    [Fact]
    public void ExitPendingIsAnOpenStatus_SoStrandingItAlsoBlocksNewSignals()
    {
        // Why the dead-end was doubly costly: OnePositionGate counts EXIT_PENDING as exposure,
        // so signal 60 would have suppressed every new signal until cleared by hand.
        Assert.True(Trading.Worker.SignalGates.OnePositionGate.IsOpenStatus(SignalStatus.ExitPending));
        Assert.False(Trading.Worker.SignalGates.OnePositionGate.IsOpenStatus(SignalStatus.OrderRejected));
    }
}
