using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The replayed OI gate uses live's OI change: OpenInterest now minus the latest snapshot at least 4 minutes older.</summary>
public class BacktestOiConfirmsTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 13, 28, 0);       // IST
    private static readonly DateTime Expiry = new(2026, 10, 20);
    private const int Interval = 50;

    private static IEnumerable<OptionMinuteRow> Chain(DateTime minute, long ceOi, long peOi, long storedChange = 0)
    {
        foreach (var strike in new[] { 23900m, 23950m, 24000m, 24050m, 24100m })
            foreach (var (type, oi) in new[] { ("CE", ceOi), ("PE", peOi) })
                yield return new OptionMinuteRow
                {
                    Symbol = "NIFTY", MinuteTime = minute, TradingSymbol = $"NIFTY{strike}{type}", StrikePrice = strike,
                    OptionType = type, ExpiryDate = Expiry, Ltp = 100, Delta = 0.5m,
                    ChangeInOi = storedChange, OpenInterest = oi, UnderlyingLtp = 24000m,
                };
    }

    private static OptionHistoryIndex Index(long ceBefore, long peBefore, long ceNow, long peNow, long storedChange = 0) =>
        new(Chain(Now.AddMinutes(-5), ceBefore, peBefore, storedChange).Concat(Chain(Now, ceNow, peNow, storedChange)));

    [Fact]
    public void PutConfirmsWhenPutOiBuildsAndThePriceFallsEvenThoughStoredChangeIsZero()
    {
        var idx = Index(ceBefore: 1000, peBefore: 1000, ceNow: 1000, peNow: 1200);            // PE +200 per strike, stored ChangeInOi = 0
        Assert.True(idx.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: -10));    // price down: long build-up on the put side
    }

    [Fact]
    public void ShortCoveringAlsoConfirmsADirectionalMove()
    {
        var idx = Index(1000, 1000, 1000, 800);                                               // PE OI falls
        Assert.True(idx.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: -10));    // oiChange < 0 with the put-direction move
    }

    [Fact]
    public void AMoveAgainstTheTradeDoesNotConfirm()
    {
        var idx = Index(1000, 1000, 1200, 1200);
        Assert.False(idx.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: +10));   // index rising: no PUT confirmation
        Assert.True(idx.OiConfirms("NIFTY", TradeSide.Call, Now, Interval, lastMove: +10));
    }

    [Fact]
    public void UnchangedOpenInterestCannotConfirm()
    {
        var idx = Index(1000, 1000, 1000, 1000);
        Assert.False(idx.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: -10));
    }

    [Fact]
    public void WithoutAnOlderSnapshotItFallsBackToTheStoredChange()
    {
        var onlyNow = new OptionHistoryIndex(Chain(Now, 1000, 1000, storedChange: 500));      // no snapshot 4+ minutes earlier
        Assert.True(onlyNow.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: -10));

        var zero = new OptionHistoryIndex(Chain(Now, 1000, 1000, storedChange: 0));
        Assert.False(zero.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: -10));
    }

    [Fact]
    public void ASnapshotLessThanFourMinutesOlderIsNotUsedAsTheBaseline()
    {
        var idx = new OptionHistoryIndex(Chain(Now.AddMinutes(-3), 1000, 1000).Concat(Chain(Now, 1000, 1500)));
        // the only earlier snapshot is 3 minutes old (< 4): no baseline, falls back to stored ChangeInOi = 0 -> not confirmed
        Assert.False(idx.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: -10));
    }

    [Fact]
    public void NoOptionHistoryIsNotReplayable()
    {
        var idx = new OptionHistoryIndex(Chain(Now.AddDays(-3), 1000, 1000));
        Assert.Null(idx.OiConfirms("NIFTY", TradeSide.Put, Now, Interval, lastMove: -10));
    }
}
