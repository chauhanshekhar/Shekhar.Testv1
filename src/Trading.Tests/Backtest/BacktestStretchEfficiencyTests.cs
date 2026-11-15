using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// The conditional stretch cap: the cap applies only while the session looks range-bound, measured by
///     efficiency = |Ltp - SessionOpen| / (DayHigh - DayLow)
/// A trending session keeps its stretched continuation entry; a chopping one does not. These assert
/// the decision table, not P&amp;L.
/// </summary>
public class BacktestStretchEfficiencyTests
{
    private static readonly DateTime T = BtSupport.At(5, 30);

    private static EntryEvaluator Eval(EntryRules rules) =>
        new(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore, rules);

    /// <summary>
    /// A PUT point stretched <paramref name="stretchAtr"/> ATRs below EMA20, on a session whose
    /// travel-from-open and high-low range are set explicitly. ATR is 20 (BtSupport's default).
    /// </summary>
    private static MarketPoint Put(double stretchAtr, double travelFromOpen, double range)
    {
        var p = BtSupport.PutPoint("BANKNIFTY", T);
        var ltp = p.Ema20 - stretchAtr * p.Atr;
        // Place the open away from LTP by travelFromOpen, and spread the day's extremes over range.
        var open = ltp + travelFromOpen;
        return p with
        {
            Ltp = ltp,
            SessionOpen = open,
            DayOpen = open,
            DayHigh = Math.Max(open, ltp) + Math.Max(0, range - Math.Abs(travelFromOpen)),
            DayLow = Math.Min(open, ltp),
        };
    }

    private static bool Blocked(MarketPoint p, EntryRules rules) =>
        Eval(rules).Evaluate(p, TradeSide.Put).BlockedBy == "Stretch";

    // Travelled the whole range in one direction: efficiency ~1.0.
    private static MarketPoint Trending(double stretchAtr) => Put(stretchAtr, travelFromOpen: 100, range: 100);

    // Travelled almost nothing against a wide range: efficiency ~0.05.
    private static MarketPoint Chopping(double stretchAtr) => Put(stretchAtr, travelFromOpen: 10, range: 200);

    [Fact]
    public void FlatCap_blocksAStretchedEntryHoweverTheSessionLooks()
    {
        var flat = new EntryRules { MaxStretchAtr = 0.85 };
        Assert.True(Blocked(Trending(stretchAtr: 2.0), flat));
        Assert.True(Blocked(Chopping(stretchAtr: 2.0), flat));
    }

    [Fact]
    public void ConditionalCap_letsATrendingSessionKeepItsStretchedEntry()
    {
        var conditional = new EntryRules
        {
            MaxStretchAtr = 0.85,
            CapOnlyWhenEfficiencyBelow = 0.35,
            EfficiencyMinRangeAtr = 0.75,
        };
        Assert.False(Blocked(Trending(stretchAtr: 2.0), conditional));
        Assert.True(Blocked(Chopping(stretchAtr: 2.0), conditional));
    }

    [Fact]
    public void WithinTheCap_nothingIsBlockedEitherWay()
    {
        var conditional = new EntryRules { MaxStretchAtr = 0.85, CapOnlyWhenEfficiencyBelow = 0.35 };
        Assert.False(Blocked(Trending(stretchAtr: 0.2), conditional));
        Assert.False(Blocked(Chopping(stretchAtr: 0.2), conditional));
    }

    [Fact]
    public void RangeTooSmallToJudge_followsCapWhenEfficiencyUnknown()
    {
        // Range 10 points against ATR 20 is below the 0.75-ATR (15-point) guard.
        var early = Put(stretchAtr: 2.0, travelFromOpen: 5, range: 10);

        Assert.True(Blocked(early, new EntryRules
        {
            MaxStretchAtr = 0.85, CapOnlyWhenEfficiencyBelow = 0.35, CapWhenEfficiencyUnknown = true,
        }));
        Assert.False(Blocked(early, new EntryRules
        {
            MaxStretchAtr = 0.85, CapOnlyWhenEfficiencyBelow = 0.35, CapWhenEfficiencyUnknown = false,
        }));
    }

    [Fact]
    public void NoCapConfigured_neverBlocksOnStretch()
    {
        Assert.False(Blocked(Chopping(stretchAtr: 3.0), new EntryRules()));
    }
}
