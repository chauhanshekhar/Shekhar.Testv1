using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>Diagnostic shared-slot replay: CALL and PUT compete for one position, and every signal minute can be logged.</summary>
public class BacktestSharedSlotTests
{
    private static ExecutionSimulator Create()
    {
        var s = BtSupport.LiveLike();
        return new ExecutionSimulator(new EntryEvaluator(s, BtSupport.Trading(), BtSupport.Instruments()), s, BtSupport.Exit(s), options: null);
    }

    private static SimulationOptions Opts(bool both, bool log) =>
        new(TradeSide.Call, StopModel.Live, PremiumMode.ModelOnly, Costs: null, BothSides: both, LogSignals: log);

    private static SymbolData TwoSignals(DateTime callAt, DateTime putAt) =>
        BtSupport.Data("NIFTY", BtSupport.FlatBars(23940), BtSupport.CallPoint("NIFTY", callAt), BtSupport.PutPoint("NIFTY", putAt));

    [Fact]
    public void AnOpenCallBlocksALaterPutWhenTheSlotIsShared()
    {
        var data = TwoSignals(BtSupport.At(4, 30), BtSupport.At(5, 0));

        var shared = Create().Run([data], Opts(both: true, log: true));
        var trade = Assert.Single(shared.Trades);
        Assert.Equal(TradeSide.Call, trade.Side);
        Assert.Equal(1, shared.BlockedBy["OnePositionGate"]);

        var blocked = Assert.Single(shared.Log, r => r.Outcome == "OnePositionGate");
        Assert.Equal("PUT", blocked.Side);
        Assert.Equal("NIFTY", blocked.OpenSymbol);
        Assert.NotNull(blocked.HeldUntilIst);                       // when the occupying position ends
        Assert.Equal(shared.TotalSignals, shared.BlockedCount + shared.Trades.Count);
    }

    [Fact]
    public void WithoutTheOptionEachSideRunsOnItsOwnAsBefore()
    {
        var data = TwoSignals(BtSupport.At(4, 30), BtSupport.At(5, 0));

        var callOnly = Create().Run([data], Opts(both: false, log: false));
        Assert.Single(callOnly.Trades);
        Assert.Empty(callOnly.Log);                                  // logging off: nothing recorded
        Assert.False(callOnly.BlockedBy.ContainsKey("OnePositionGate"));
    }

    [Fact]
    public void AnEnteredSignalIsLoggedWithItsExit()
    {
        var data = TwoSignals(BtSupport.At(4, 30), BtSupport.At(9, 0));

        var r = Create().Run([data], Opts(both: true, log: true));
        var entered = Assert.Single(r.Log, x => x.Outcome == "ENTERED" && x.Side == "CALL");
        Assert.False(string.IsNullOrEmpty(entered.ExitReason));
        Assert.NotNull(entered.ExitIst);
        Assert.NotNull(entered.PnlRs);
    }
}
