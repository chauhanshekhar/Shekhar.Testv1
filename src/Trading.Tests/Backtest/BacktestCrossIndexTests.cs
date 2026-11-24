using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Domain;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>CrossIndexAgreementGate replayed from the other indices' closed 1m bars (closed-bar rule, feed gaps, side switch).</summary>
public class BacktestCrossIndexTests
{
    private static readonly DateTime Now = BtSupport.At(5, 30);

    /// <summary>1m bars ending the minute before <paramref name="end"/>, moving linearly by <paramref name="perMinute"/>.</summary>
    private static Bar[] Series(DateTime end, int count, double start, double perMinute) =>
        Enumerable.Range(0, count).Select(i =>
        {
            var open = end.AddMinutes(-(count - i));
            var c = start + perMinute * i;
            return new Bar(open, c, c + 1, c - 1, c);
        }).ToArray();

    private static CrossIndexReplay Replay(Bar[]? nifty, Bar[]? sensex, params string[] active)
    {
        var series = new List<(string, Bar[])>();
        if (nifty is not null) series.Add(("NIFTY", nifty));
        if (sensex is not null) series.Add(("SENSEX", sensex));
        return new CrossIndexReplay(series, active);
    }

    private static string? Block(CrossIndexReplay r, string symbol, string type, int side = 0) =>
        r.Block(symbol, type, Now, side, lookback: 15, minAgree: 2);

    [Fact]
    public void Call_Passes_WhenBothOthersAreUp_AndBlocks_WhenOneIsDown()
    {
        var up = Series(Now, 30, 24000, +2);
        var down = Series(Now, 30, 24000, -2);

        Assert.Null(Block(Replay(up, up, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.CALL));
        Assert.NotNull(Block(Replay(up, down, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.CALL));
    }

    [Fact]
    public void Put_NeedsTheOthersDown()
    {
        var up = Series(Now, 30, 24000, +2);
        var down = Series(Now, 30, 24000, -2);

        Assert.Null(Block(Replay(down, down, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.PUT));
        Assert.NotNull(Block(Replay(up, up, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.PUT));
    }

    [Fact]
    public void OnlyActiveOtherIndicesCount()
    {
        var up = Series(Now, 30, 24000, +2);
        var down = Series(Now, 30, 24000, -2);
        // NIFTY is falling but not active (like today's TradingInstruments): only SENSEX is compared
        Assert.Null(Block(Replay(down, up, "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.CALL));
    }

    [Fact]
    public void AnIndexWithNoData_IsNotCounted_AndNoDataAtAllLetsTheSignalThrough()
    {
        var down = Series(Now, 30, 24000, -2);
        Assert.NotNull(Block(Replay(null, down, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.CALL));
        Assert.Null(Block(Replay(null, null, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.CALL));
    }

    [Fact]
    public void StaleBars_CountAsNoData()
    {
        // the newest bar of the falling index opened 10 minutes ago: live treats a stopped feed as no data
        var stale = Series(Now.AddMinutes(-10), 30, 24000, -2);
        Assert.Null(Block(Replay(stale, stale, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.CALL));
    }

    [Fact]
    public void TheFormingBarIsNeverRead()
    {
        // all closed bars are flat-up; a bar opening AT the decision minute (still forming) crashes - it must not flip the result
        var up = Series(Now, 30, 24000, +2).Append(new Bar(Now, 24060, 24061, 23000, 23000)).ToArray();
        Assert.Null(Block(Replay(up, up, "NIFTY", "SENSEX", "BANKNIFTY"), "BANKNIFTY", TradingConst.SignalType.CALL));
    }

    [Theory]
    [InlineData(1, "CALL", true)]     // CALL only: a CALL is checked
    [InlineData(1, "PUT", false)]     // CALL only: a PUT is not
    [InlineData(2, "CALL", false)]    // PUT only: a CALL is not checked
    [InlineData(2, "PUT", true)]
    public void TheSideSwitchLimitsWhichDirectionIsChecked(int side, string type, bool checkedSide)
    {
        // others fall: a CALL would be blocked, a PUT would pass; flip for the PUT case so each checked side is a block
        var others = type == "CALL" ? Series(Now, 30, 24000, -2) : Series(Now, 30, 24000, +2);
        var r = Replay(others, others, "NIFTY", "SENSEX", "BANKNIFTY");
        Assert.Equal(checkedSide, Block(r, "BANKNIFTY", type, side) is not null);
    }
}
