using Trading.Worker;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// NoTradeWindowGate was written (with its StrategySettings rows) but never registered in SignalWorker.PostGates, so
/// production ignored EnableNoTradeWindowGate while the backtest applied it. It must be in the live post-gate list,
/// right after EarlyTrendGate - the order the backtest's EntryEvaluator applies the same checks in.
/// </summary>
public class NoTradeWindowGateRegistrationTests
{
    [Fact]
    public void PostGates_containNoTradeWindowGate_exactlyOnce()
    {
        Assert.Single(SignalWorker.PostGates, g => g is NoTradeWindowGate);
    }

    [Fact]
    public void NoTradeWindowGate_runsRightAfterEarlyTrendGate()
    {
        var gates = SignalWorker.PostGates;
        var earlyTrend = Array.FindIndex(gates, g => g is EarlyTrendGate);
        var window = Array.FindIndex(gates, g => g is NoTradeWindowGate);

        Assert.True(earlyTrend >= 0);
        Assert.Equal(earlyTrend + 1, window);
    }
}
