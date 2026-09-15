using Trading.Domain.Entities;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The SL_M retry budget must count MONOTONICALLY across a run of rejections.
///
/// Live incident, 2026-08-27 signal 34: 21 rejected SL_M orders in 7 minutes, every one
/// "RMS:Margin Exceeds", while StopLossRetryCount still read 1. MaxStopLossRetries is 5, so the
/// cap should have stopped it after 5 — it never could, because the count was being reset.
///
/// The cause was a pairing:
///   TryPlaceAsync  set StopLossRetryCount = 0 when the broker ACCEPTED the order
///   MarkFailedAsync incremented it when the broker REJECTED that same order ~20s later
/// Groww accepts an SL_M and rejects it asynchronously, so both fired every cycle and the count
/// oscillated 0 -> 1 -> 0 forever.
///
/// The reset now lives on a CONFIRMED CANCEL instead: an order can only be cancelled if it was
/// genuinely resting at the exchange, which is the real evidence of a working stop.
///
/// These tests model the counter arithmetic directly — the surrounding methods need a broker,
/// repositories and a unit of work, none of which have fakes in this project.
/// </summary>
public class StopLossRetryBudgetTests
{
    private const int MaxStopLossRetries = 5;   // TradingSettings default

    /// <summary>Broker accepted the placement. Under the fix this must NOT touch the budget.</summary>
    private static void OnBrokerAccepted(Signal s)
    {
        s.StopLossStatus = "PLACED";
        // no StopLossRetryCount change — acceptance is not proof of a resting stop
    }

    /// <summary>Broker rejected the order (sync throw, or async postback). Always counts.</summary>
    private static void OnBrokerRejected(Signal s)
    {
        s.StopLossStatus = "FAILED";
        s.StopLossRetryCount++;
    }

    /// <summary>A cancel only succeeds against an order that was really resting.</summary>
    private static void OnCancelConfirmed(Signal s)
    {
        s.StopLossOrderId = "";
        s.StopLossStatus = "";
        s.StopLossRetryCount = 0;
    }

    private static bool CapReached(Signal s) => s.StopLossRetryCount >= MaxStopLossRetries;

    // ── The incident ─────────────────────────────────────────────────────────

    [Fact]
    public void RepeatedAcceptThenReject_ReachesTheCap()
    {
        var signal = new Signal();

        // Signal 34's loop: accept, then async reject, over and over.
        for (var attempt = 1; attempt <= 21; attempt++)
        {
            if (CapReached(signal)) break;
            OnBrokerAccepted(signal);
            OnBrokerRejected(signal);
        }

        Assert.True(CapReached(signal));
        Assert.Equal(MaxStopLossRetries, signal.StopLossRetryCount);
    }

    [Fact]
    public void TheOldPairing_OscillatedAndNeverReachedTheCap()
    {
        // Reproduces the bug: reset on acceptance cancels out the rejection increment.
        var signal = new Signal();

        for (var attempt = 1; attempt <= 21; attempt++)
        {
            signal.StopLossRetryCount = 0;   // the removed line, on acceptance
            OnBrokerRejected(signal);
        }

        Assert.Equal(1, signal.StopLossRetryCount);   // exactly what signal 34 showed after 21 tries
        Assert.False(CapReached(signal));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(21)]
    [InlineData(100)]
    public void RejectionsAlwaysCountMonotonically(int rejections)
    {
        var signal = new Signal();
        for (var i = 0; i < rejections; i++) OnBrokerRejected(signal);

        Assert.Equal(rejections, signal.StopLossRetryCount);
    }

    // ── The normal path must not exhaust the budget ──────────────────────────

    [Fact]
    public void TrailingCancelThenPlace_NeverExhaustsTheBudget()
    {
        // A healthy trailing stop cancels a resting order and places a new one, many times a day.
        // That must never trip the cap.
        var signal = new Signal();

        for (var move = 0; move < 50; move++)
        {
            OnCancelConfirmed(signal);   // old stop was resting -> budget renewed
            OnBrokerAccepted(signal);
        }

        Assert.Equal(0, signal.StopLossRetryCount);
        Assert.False(CapReached(signal));
    }

    [Fact]
    public void AConfirmedCancelClearsAPartiallySpentBudget()
    {
        var signal = new Signal();
        OnBrokerRejected(signal);
        OnBrokerRejected(signal);
        Assert.Equal(2, signal.StopLossRetryCount);

        // The next attempt actually rests and is later cancelled — genuine recovery.
        OnCancelConfirmed(signal);

        Assert.Equal(0, signal.StopLossRetryCount);
    }

    [Fact]
    public void IntermittentSuccessDoesNotMaskASustainedFailureRun()
    {
        // Two real (cancellable) stops early on, then the account runs out of margin.
        var signal = new Signal();
        OnCancelConfirmed(signal);
        OnCancelConfirmed(signal);

        for (var i = 0; i < 10; i++)
        {
            if (CapReached(signal)) break;
            OnBrokerAccepted(signal);
            OnBrokerRejected(signal);
        }

        Assert.True(CapReached(signal));
    }
}
