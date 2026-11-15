using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Signal 10291 (BANKNIFTY PUT, paper, 27 Oct 2026): ExitWorker trailed OptionSL 67.46 -> 82.86 and
/// step-locked ProtectedSL against the STALE 82.80 signal quote while the entry was still OrderPlaced.
/// The fill (109.80) then re-anchored the stop by ratio, giving 109.88 instead of ~89.4, and the trade
/// was stopped out 4 s after entry, before the premium ran past T1.
/// </summary>
public class UnfilledEntryStopTests
{
    [Fact]
    public void OrderPlaced_IsSkipped_OnNormalCycle()
        => Assert.True(ExitWorker.ShouldSkipUnfilledEntry(SignalStatus.OrderPlaced, forceExit: false));

    [Fact]
    public void OrderPlaced_StillRuns_OnForceExit()
        => Assert.False(ExitWorker.ShouldSkipUnfilledEntry(SignalStatus.OrderPlaced, forceExit: true));

    [Theory]
    [InlineData(SignalStatus.Active)]
    [InlineData(SignalStatus.PartialExitPending)]
    public void FilledSignals_AreNeverSkipped(string status)
        => Assert.False(ExitWorker.ShouldSkipUnfilledEntry(status, forceExit: false));

    [Fact]
    public void Fill_ResetsExcursionAndLockedStop_ToTheFillPrice()
    {
        var signal = new Signal
        {
            MfePrice = 114.20m, MaePrice = 82.80m, ProtectedSL = 101.20m, IndexT1Hit = true,
        };

        BrokerOrderSyncWorker.ResetExcursionAtFill(signal, 109.80m);

        Assert.Equal(109.80m, signal.MfePrice);
        Assert.Equal(109.80m, signal.MaePrice);
        Assert.Equal(0m, signal.ProtectedSL);
        Assert.False(signal.IndexT1Hit);
    }

    [Fact]
    public void Fill_WithNoPrice_LeavesSignalUntouched()
    {
        var signal = new Signal { MfePrice = 114.20m, ProtectedSL = 101.20m };

        BrokerOrderSyncWorker.ResetExcursionAtFill(signal, 0m);

        Assert.Equal(114.20m, signal.MfePrice);
        Assert.Equal(101.20m, signal.ProtectedSL);
    }
}
