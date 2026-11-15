using Trading.Domain;
using Trading.SignalEngine.Services;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Signal 10316 (BANKNIFTY PUT, paper, 2 Nov 2026): a PARTIAL_T1 SELL was pending when ExitWorker's
/// paper path booked a full PROTECTED_SL_HIT exit for all 4 lots and closed the signal; 100 ms later
/// the partial fill set it back to ACTIVE, where it stayed with 6 lots booked on a 4-lot trade.
/// </summary>
public class PendingPartialExitTests
{
    [Fact]
    public void PaperExit_IsDeferred_WhilePartialExitPending()
        => Assert.True(ExitWorker.ShouldDeferPaperExit(SignalStatus.PartialExitPending));

    [Fact]
    public void PaperExit_Runs_WhenActive()
        => Assert.False(ExitWorker.ShouldDeferPaperExit(SignalStatus.Active));

    [Fact]
    public void PartialFill_DoesNotReopen_ClosedSignal()
        => Assert.Equal(SignalStatus.Closed, TradeCompletionService.StatusAfterPartialFill(SignalStatus.Closed));

    [Theory]
    [InlineData(SignalStatus.PartialExitPending)]
    [InlineData(SignalStatus.Active)]
    public void PartialFill_KeepsOpenTrade_Active(string status)
        => Assert.Equal(SignalStatus.Active, TradeCompletionService.StatusAfterPartialFill(status));
}
