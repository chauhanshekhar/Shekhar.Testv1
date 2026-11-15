using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The previous-5-minute level condition, replayed through the live EvaluateCall / EvaluatePut once Prev5mHigh/Prev5mLow are carried on the point.</summary>
public class BacktestBreakoutLevelTests
{
    private static readonly DateTime T = BtSupport.At(4, 30);      // 10:00 IST

    private static EntryEvaluator Eval(bool callLevelOn = false, bool putLevelOn = false)
    {
        var s = BtSupport.LiveLike();
        if (callLevelOn) s.Set("EnableCallPrev5mHighFilter", null, 1);
        if (putLevelOn) s.Set("EnablePutPrev5mLowFilter", null, 1);
        return new EntryEvaluator(s, BtSupport.Trading(), BtSupport.Instruments());
    }

    [Fact]
    public void WithTheFilterOffTheLevelIsIgnored()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { Prev5mHigh = 1_000_000 };
        Assert.True(Eval().Evaluate(p, TradeSide.Call).Candidate);                 // live rules: filter off, unchanged
    }

    [Theory]
    [InlineData(23939.0, true)]      // LTP (23940 for CallPoint) closes above the 5m high
    [InlineData(23940.0, false)]     // equal is not a break
    [InlineData(24500.0, false)]
    public void CallNeedsAClosedPriceAboveThePrevious5mHighWhenOn(double prev5mHigh, bool expected)
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { Prev5mHigh = prev5mHigh };
        Assert.Equal(expected, Eval(callLevelOn: true).Evaluate(p, TradeSide.Call).Candidate);
    }

    [Fact]
    public void AMissingLevelFailsClosedWhenOn()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { Prev5mHigh = 0 };
        Assert.False(Eval(callLevelOn: true).Evaluate(p, TradeSide.Call).Candidate);
    }

    [Theory]
    [InlineData(1000000.0, true)]    // far above LTP: the PUT closes below the 5m low
    [InlineData(0.0, false)]         // 0 = unavailable, fails closed
    public void PutNeedsAClosedPriceBelowThePrevious5mLowWhenOn(double prev5mLow, bool expected)
    {
        var p = BtSupport.PutPoint("NIFTY", T) with { Prev5mLow = prev5mLow };
        Assert.Equal(expected, Eval(putLevelOn: true).Evaluate(p, TradeSide.Put).Candidate);
    }
}
