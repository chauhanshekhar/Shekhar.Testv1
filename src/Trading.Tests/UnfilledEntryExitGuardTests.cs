using Trading.Domain;
using Trading.Worker.Exits;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The exit path must never place a SELL for an entry that never filled.
///
/// Live incident, 2026-08-27: signals 23 and 31 had their BUY entry CANCELLED / REJECTED with
/// FilledQty=0, yet the system still placed a TARGET sell (order 207) and an EXIT sell (order
/// 224) against them. Both were rejected by the broker — but only because the account was out of
/// margin. With funds they would have filled and OPENED short positions that were never intended.
///
/// SignalStatus.OrderPlaced means "entry order sent", NOT "entry filled": only
/// BrokerOrderSyncWorker.ActivateSignalAsync promotes to Active, and solely on a BUY-fill
/// confirmation. ExitDataLoader still loads OrderPlaced signals on purpose (a real fill whose
/// confirmation is late must remain exitable), so the exclusion has to live at the point of sale.
/// </summary>
public class UnfilledEntryExitGuardTests
{
    // ── Statuses only reachable after a confirmed fill ───────────────────────

    [Theory]
    [InlineData(SignalStatus.Active)]
    [InlineData(SignalStatus.PartialExitPending)]
    [InlineData(SignalStatus.ExitPending)]
    [InlineData(SignalStatus.CloseFailed)]
    public void FilledStatuses_AllowTheSell(string status)
    {
        Assert.True(PositionCloseService.IsEntryFillConfirmed(status, entryFilledQty: 0));
    }

    // ── The incident: entry never filled ─────────────────────────────────────

    [Theory]
    [InlineData(SignalStatus.OrderPlaced)]     // entry sent, no fill confirmation
    [InlineData(SignalStatus.Pending)]
    [InlineData(SignalStatus.OrderRejected)]   // signal 31 — entry REJECTED
    [InlineData(SignalStatus.OrderUnknown)]
    public void UnfilledStatuses_BlockTheSell(string status)
    {
        Assert.False(PositionCloseService.IsEntryFillConfirmed(status, entryFilledQty: 0));
    }

    [Fact]
    public void Signal23_CancelledEntryWithZeroFill_IsBlocked()
    {
        // Order 205: BUY LIMIT, CANCELLED, FilledQty=0 — then a TARGET sell was placed anyway.
        Assert.False(PositionCloseService.IsEntryFillConfirmed(
            SignalStatus.OrderPlaced, entryFilledQty: 0));
    }

    [Fact]
    public void Signal31_RejectedEntryWithZeroFill_IsBlocked()
    {
        // Order 222: BUY LIMIT, REJECTED (margin), FilledQty=0 — then order 224 SELL MARKET EXIT.
        Assert.False(PositionCloseService.IsEntryFillConfirmed(
            SignalStatus.OrderRejected, entryFilledQty: 0));
    }

    // ── A late fill confirmation must not strand a real position ─────────────

    [Theory]
    [InlineData(SignalStatus.OrderPlaced)]
    [InlineData(SignalStatus.Pending)]
    public void LateConfirmation_ButEntryDidFill_AllowsTheSell(string status)
    {
        // Status hasn't advanced yet, but the entry order shows a real fill — the position is
        // genuinely open and MUST stay exitable, so this may not be blocked.
        Assert.True(PositionCloseService.IsEntryFillConfirmed(status, entryFilledQty: 30));
    }

    [Fact]
    public void PartialFill_CountsAsFilled()
    {
        Assert.True(PositionCloseService.IsEntryFillConfirmed(
            SignalStatus.OrderPlaced, entryFilledQty: 1));
    }
}
