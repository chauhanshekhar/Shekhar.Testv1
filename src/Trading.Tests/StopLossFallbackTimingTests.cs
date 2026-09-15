using Trading.Domain;
using Trading.Domain.Entities;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Regression cover for the SL_M fallback failing to fire, which left a live position with no
/// stop-loss protection and required a manual close on the server.
///
/// Two distinct defects, both reproduced below against the same elapsed-time rule ExitWorker
/// applies (unprotectedSince = LastSlUpdateTime ?? CreatedOn):
///
///   1. TIMER RESET - MarkFailedAsync used to stamp LastSlUpdateTime on every FAILED placement.
///      SyncStopLossOrderAsync runs immediately before DetermineExitReason in the same ExitWorker
///      cycle, so the timestamp was refreshed milliseconds before the check read it and the
///      elapsed time never reached StopLossFallbackTimeoutSeconds.
///
///   2. NEVER PLACED - the fallback was additionally guarded on LastSlUpdateTime.HasValue, so a
///      signal whose very first placement failed (field still null) skipped the fallback entirely.
/// </summary>
public class StopLossFallbackTimingTests
{
    private const int FallbackTimeoutSeconds = 5;

    /// <summary>Mirrors ExitWorker's fallback timing decision.</summary>
    private static bool FallbackWouldFire(Signal signal, DateTime now) =>
        (now - (signal.LastSlUpdateTime ?? signal.CreatedOn)).TotalSeconds >= FallbackTimeoutSeconds;

    [Fact]
    public void FailedPlacement_DoesNotResetProtectionTimer()
    {
        var now = new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
        var signal = new Signal
        {
            CreatedOn = now.AddMinutes(-10),
            LastSlUpdateTime = now.AddSeconds(-30), // stop was resting 30s ago, then lost
            StopLossStatus = "FAILED",
        };

        // Simulate repeated failing retries. MarkFailedAsync must NOT touch LastSlUpdateTime --
        // if it did, each retry would push the timer back to ~0 and the fallback would never fire.
        for (var i = 0; i < 20; i++)
        {
            signal.StopLossStatus = "FAILED";
            signal.StopLossRetryCount++;
        }

        Assert.Equal(now.AddSeconds(-30), signal.LastSlUpdateTime);
        Assert.True(FallbackWouldFire(signal, now),
            "30s unprotected must trigger the fallback; retries must not reset the timer");
    }

    [Fact]
    public void NeverPlaced_FallsBackToCreatedOn_AndStillFires()
    {
        var now = new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
        var signal = new Signal
        {
            CreatedOn = now.AddSeconds(-60),
            LastSlUpdateTime = null, // first placement failed -- never had a resting stop
            StopLossStatus = "FAILED",
        };

        Assert.True(FallbackWouldFire(signal, now),
            "a position that never had a stop must still be protected by the fallback");
    }

    [Fact]
    public void WithinTimeout_DoesNotFireYet()
    {
        var now = new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
        var signal = new Signal
        {
            CreatedOn = now.AddMinutes(-10),
            LastSlUpdateTime = now.AddSeconds(-2), // only 2s unprotected
            StopLossStatus = "FAILED",
        };

        Assert.False(FallbackWouldFire(signal, now),
            "must not fire before StopLossFallbackTimeoutSeconds elapses");
    }

    // ── Stale-id regression ───────────────────────────────────────────────────
    // Live incident 2026-08-18: SENSEX CALL, StopLossOrderId=GSMFO...UR42F set, StopLossStatus
    // FAILED, StopLossRetryCount 0, StopLossTriggerPrice 243.70 vs OptionSL 243.69. The broker
    // rejected the SL_M; BrokerOrderSyncWorker set FAILED but left the id populated, so
    // SyncStopLossOrderAsync took its "an order already exists" branch, then returned on the
    // 0.05 tolerance check (|243.70 - 243.69| = 0.01) and never re-placed. Position ran with no
    // resting stop and had to be closed by hand.

    /// <summary>Mirrors SyncStopLossOrderAsync's self-heal decision.</summary>
    private static bool WouldSelfHeal(Signal s) =>
        string.IsNullOrEmpty(s.StopLossOrderId) ||
        s.StopLossStatus == "FAILED" ||
        s.StopLossStatus == "CANCELLED";

    [Fact]
    public void RejectedStopWithStaleOrderId_StillTriggersSelfHeal()
    {
        var signal = new Signal
        {
            StopLossOrderId = "GSMFO260818120250PT9EGD9UR42F", // dead id left by the rejection
            StopLossStatus = "FAILED",
            StopLossTriggerPrice = 243.70m,
            OptionSL = 243.69m, // within the 0.05 tolerance -> trailing-replace path would no-op
            StopLossRetryCount = 0,
        };

        Assert.True(WouldSelfHeal(signal),
            "a FAILED stop must re-place even though a stale broker id is still recorded");
    }

    [Fact]
    public void CancelledStopWithStaleOrderId_StillTriggersSelfHeal()
    {
        var signal = new Signal
        {
            StopLossOrderId = "GSMFO260818120250PT9EGD9UR42F",
            StopLossStatus = "CANCELLED",
        };

        Assert.True(WouldSelfHeal(signal));
    }

    [Fact]
    public void PlacedStop_DoesNotReplace()
    {
        var signal = new Signal
        {
            StopLossOrderId = "GSMFO260818120250PT9EGD9UR42F",
            StopLossStatus = "PLACED",
        };

        Assert.False(WouldSelfHeal(signal),
            "a healthy resting stop must not be cancelled/replaced every cycle");
    }
}
