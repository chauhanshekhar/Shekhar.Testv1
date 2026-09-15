using Trading.Domain;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Which signal statuses count as an open position.
///
/// Regression cover for 2026-08-24: the gate inferred "open" from the absence of a TradeJournal
/// row, so signal 18 — stuck at ORDER_REJECTED, a status that can never produce a journal row —
/// suppressed every new signal on SENSEX, BANKNIFTY and NIFTY for the rest of the day. Only a
/// hand-written journal insert released it.
///
/// The rule now: real exposure blocks, dead states do not.
/// </summary>
public class OnePositionGateStatusTests
{
    [Theory]
    [InlineData(SignalStatus.OrderPlaced)]
    [InlineData(SignalStatus.Active)]
    [InlineData(SignalStatus.PartialExitPending)]
    [InlineData(SignalStatus.ExitPending)]   // exit in flight — still exposed until confirmed
    public void RealExposure_Blocks(string status)
    {
        Assert.True(OnePositionGate.IsOpenStatus(status));
    }

    [Theory]
    [InlineData(SignalStatus.OrderRejected)]        // the 2026-08-24 incident
    [InlineData(SignalStatus.Closed)]
    [InlineData(SignalStatus.CloseFailed)]
    [InlineData(SignalStatus.OrderUnknown)]
    [InlineData(SignalStatus.ManualCheckRequired)]
    public void DeadStates_DoNotBlock(string status)
    {
        Assert.False(OnePositionGate.IsOpenStatus(status),
            $"{status} can never produce a TradeJournal row; treating it as open halted all trading for a day");
    }

    [Fact]
    public void Pending_DoesNotBlock()
    {
        // A signal sits at PENDING only while placement is in flight. Counting it would block on
        // every in-flight order, including the one currently being placed.
        Assert.False(OnePositionGate.IsOpenStatus(SignalStatus.Pending));
    }

    [Fact]
    public void TheIncidentStatus_NoLongerHaltsTrading()
    {
        // Signal 18 was ORDER_REJECTED with a real filled position behind it. Whatever else is
        // wrong with that row, it must not be what stops NIFTY and SENSEX from trading.
        Assert.False(OnePositionGate.IsOpenStatus(SignalStatus.OrderRejected));
    }
}
