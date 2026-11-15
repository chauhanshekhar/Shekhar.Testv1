using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// The candidate entry condition read from IndicatorSnapshots features: opening type, 1m RSI band, ATR% window and EMA alignment,
/// in "replaces the chain" and "and the chain" modes. Default rules must leave production behaviour untouched.
/// </summary>
public class BacktestProposedEntryTests
{
    private static readonly DateTime T = BtSupport.At(5, 30);

    private static EntryEvaluator Eval(EntryRules rules, bool floorOff)
    {
        var settings = BtSupport.LiveLike();
        if (floorOff) settings.Set("EnableIndexStopFloor", null, 0m);
        return new(settings, BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore, rules);
    }

    private static ProposedEntryRule C1(bool replace) => new()
    {
        Opening = OpeningType.GapDown, Rsi1mFloor = 45, Rsi1mCeiling = 55, AtrPctMin = 0, AtrPctMax = 0.03,
        Alignment = EmaAlignment.Bearish, ReplacesChain = replace,
    };

    /// <summary>A PUT point that satisfies C1: gap-down, 1m RSI 50, ATR 0.02%, price below EMA20 below EMA50, 5m EMA20 below EMA50.</summary>
    private static MarketPoint Match() =>
        BtSupport.PutPoint("SENSEX", T, rsi5: 50) with
        {
            OpenGapPct = -0.8, Rsi1m = 50, AtrPct = 0.02, Atr = 15,
            Ltp = 100, Ema20 = 101, Ema50 = 102, Ema20_5m = 101, Ema50_5m = 102,
        };

    private static EntryVerdict Run(MarketPoint p, EntryRules rules, bool floorOff = true) =>
        Eval(rules, floorOff).Evaluate(p, TradeSide.Put);

    [Fact]
    public void Replace_acceptsAMatchingPoint_evenWhenTheLiveChainWouldReject()
    {
        var p = Match();
        Assert.False(Run(p, new EntryRules()).Candidate);   // 5m RSI 50 is outside the live PUT band
        var v = Run(p, new EntryRules { Proposed = C1(true) });
        Assert.True(v.Candidate);
        Assert.Equal("PROPOSED_ENTRY", v.Path);
    }

    [Theory]
    [InlineData(-0.1, 50, 0.02)]    // not a gap-down session
    [InlineData(-0.8, 55, 0.02)]    // RSI ceiling is exclusive
    [InlineData(-0.8, 44.9, 0.02)]  // below the RSI floor
    [InlineData(-0.8, 50, 0.03)]    // ATR maximum is exclusive
    public void Replace_rejectsEachConditionOnItsOwn(double gap, double rsi1m, double atrPct)
    {
        var p = Match() with { OpenGapPct = gap, Rsi1m = rsi1m, AtrPct = atrPct };
        var v = Run(p, new EntryRules { Proposed = C1(true) });
        Assert.False(v.Candidate);
        Assert.Equal("ProposedEntry", v.BlockedBy);
    }

    [Fact]
    public void Alignment_bearishNeedsPriceBelowEma20_andTheFiveMinuteEmasAgreeing()
    {
        Assert.False(Run(Match() with { Ltp = 101.5 }, new EntryRules { Proposed = C1(true) }).Candidate);    // above EMA20 = mixed
        Assert.False(Run(Match() with { Ema20_5m = 103 }, new EntryRules { Proposed = C1(true) }).Candidate); // 5m disagrees = mixed
        var mixed = C1(true) with { Alignment = EmaAlignment.Mixed };
        Assert.True(Run(Match() with { Ltp = 101.5 }, new EntryRules { Proposed = mixed }).Candidate);
    }

    [Fact]
    public void AndChain_stillNeedsTheLiveChainToPass()
    {
        var v = Run(Match(), new EntryRules { Proposed = C1(false) });
        Assert.False(v.Candidate);                          // 5m RSI 50 fails the live band
        Assert.Equal("AndChain", v.BlockedBy);
    }

    [Fact]
    public void Replace_keepsTheIndexStopFloor()
    {
        var v = Run(Match() with { Atr = 1 }, new EntryRules { Proposed = C1(true) }, floorOff: false);
        Assert.False(v.Candidate);
        Assert.Equal("IndexStopFloorGate", v.BlockedBy);
    }

    [Fact]
    public void WithNoProposedRule_behaviourIsUnchanged()
    {
        Assert.False(Run(Match(), new EntryRules()).Candidate);
        Assert.True(Run(BtSupport.PutPoint("SENSEX", T, rsi5: 33), new EntryRules()).Candidate);
    }
}
