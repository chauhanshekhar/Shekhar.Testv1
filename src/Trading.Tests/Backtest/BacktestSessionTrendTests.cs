using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// The session-trend rule: "gap down and going down and down", and its siblings. The session must
/// have travelled far enough in the trade's direction WITHOUT round-tripping, measured from running
/// values only. These assert the decision table, not P&amp;L.
/// </summary>
public class BacktestSessionTrendTests
{
    private static readonly DateTime T = BtSupport.At(5, 30);

    private static EntryEvaluator Eval(EntryRules rules) =>
        new(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore, rules);

    /// <summary>
    /// A PUT candidate. ATR is 20 (BtSupport default), so 0.75 ATR = 15 points.
    /// <paramref name="downFromOpen"/> is how far below the session open price sits, in points;
    /// <paramref name="range"/> is the session high-low so far; <paramref name="offLow"/> is how far
    /// above the session low price currently is.
    /// </summary>
    private static MarketPoint Put(double downFromOpen, double range, double offLow = 0, double gapPct = -0.80)
    {
        var p = BtSupport.PutPoint("BANKNIFTY", T);
        var open = p.Ltp + downFromOpen;
        var low = p.Ltp - offLow;
        return p with
        {
            SessionOpen = open,
            DayOpen = open,
            DayLow = low,
            DayHigh = low + range,
            OpenGapPct = gapPct,
        };
    }

    private static readonly EntryRules GoingDown = new()
    {
        SessionTrendMinMoveAtr = 0.75,
        SessionTrendMinEfficiency = 0.50,
    };

    private static string? Block(MarketPoint p, EntryRules r) => Eval(r).Evaluate(p, TradeSide.Put).BlockedBy;

    [Fact]
    public void ASessionTravellingDownCleanly_passes()
    {
        // 40 points below the open (2 ATR) on a 45-point range => efficiency 0.89.
        Assert.Null(Block(Put(downFromOpen: 40, range: 45), GoingDown));
    }

    [Fact]
    public void ASessionThatHasNotMovedFarEnough_isBlocked()
    {
        // 10 points = 0.5 ATR, below the 0.75 ATR minimum.
        Assert.Equal("SessionTrend", Block(Put(downFromOpen: 10, range: 45), GoingDown));
    }

    [Fact]
    public void ASessionThatRoundTripped_isBlocked_evenThoughItTravelled()
    {
        // 20 points below the open (1 ATR, enough) but on a 200-point range => efficiency 0.10.
        Assert.Equal("SessionTrend", Block(Put(downFromOpen: 20, range: 200), GoingDown));
    }

    [Fact]
    public void AnUpwardSession_isBlockedForAPut()
    {
        // Price ABOVE the session open: trendSoFar is negative for a PUT.
        Assert.Equal("SessionTrend", Block(Put(downFromOpen: -40, range: 45), GoingDown));
    }

    [Fact]
    public void NearExtreme_blocksASessionThatHasStalledAfterTheMove()
    {
        var withExtreme = GoingDown with { SessionTrendNearExtremeAtr = 1.0 };

        // Still at the low: passes.
        Assert.Null(Block(Put(downFromOpen: 40, range: 45, offLow: 0), withExtreme));
        // Bounced 40 points (2 ATR) off the low: blocked.
        Assert.Equal("SessionTrend", Block(Put(downFromOpen: 40, range: 45, offLow: 40), withExtreme));
        // Without the option set, the same point passes.
        Assert.Null(Block(Put(downFromOpen: 40, range: 45, offLow: 40), GoingDown));
    }

    [Fact]
    public void ScopedToAnOpening_itIgnoresOtherOpenings()
    {
        var gapDownOnly = GoingDown with { SessionTrendAppliesTo = OpeningType.GapDown };

        // A weak session that WOULD be blocked, but the day opened normal, so the rule is out of scope.
        Assert.Null(Block(Put(downFromOpen: 10, range: 45, gapPct: 0.05), gapDownOnly));
        // Same weak session on a gap-down open: in scope, blocked.
        Assert.Equal("SessionTrend", Block(Put(downFromOpen: 10, range: 45, gapPct: -0.80), gapDownOnly));
    }

    [Fact]
    public void WithTheRuleUnset_behaviourIsUnchanged()
    {
        Assert.Null(Block(Put(downFromOpen: 0, range: 200), new EntryRules()));
    }
}
