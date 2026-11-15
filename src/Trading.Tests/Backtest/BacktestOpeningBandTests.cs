using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// The opening-conditional RSI band: a different band on sessions that gapped. The gap is known at
/// the open, so this is a legitimate entry rule (unlike the continuation/reversal label).
/// These assert that the swap happens on the right sessions and nowhere else.
/// </summary>
public class BacktestOpeningBandTests
{
    private static readonly DateTime T = BtSupport.At(5, 30);

    private static EntryEvaluator Eval(EntryRules rules) =>
        new(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore, rules);

    /// <summary>A PUT candidate at the given 5m RSI on a session that opened with the given gap.</summary>
    private static MarketPoint Put(double rsi5, double gapPct) =>
        BtSupport.PutPoint("BANKNIFTY", T, rsi5: rsi5) with { OpenGapPct = gapPct };

    private static readonly EntryRules GapUp45To60 = new()
    {
        OpeningBandAppliesTo = OpeningType.GapUp,
        OpeningBandRsiFloor = 45m,
        OpeningBandRsiCeiling = 60m,
    };

    private static bool Passes(MarketPoint p, EntryRules rules) =>
        Eval(rules).Evaluate(p, TradeSide.Put).Candidate;

    [Fact]
    public void OnAGapUpSession_theOverrideBandApplies_andTheLiveBandDoesNot()
    {
        // RSI 50 is outside the live PUT band (28-38) but inside the gap-up override (45-60).
        Assert.False(Passes(Put(rsi5: 50, gapPct: 0.80), new EntryRules()));
        Assert.True(Passes(Put(rsi5: 50, gapPct: 0.80), GapUp45To60));

        // RSI 33 is inside the live band but outside the override, so the override must reject it.
        Assert.True(Passes(Put(rsi5: 33, gapPct: 0.80), new EntryRules()));
        Assert.False(Passes(Put(rsi5: 33, gapPct: 0.80), GapUp45To60));
    }

    [Fact]
    public void OnANormalOpen_theLiveBandStillApplies()
    {
        Assert.True(Passes(Put(rsi5: 33, gapPct: 0.05), GapUp45To60));
        Assert.False(Passes(Put(rsi5: 50, gapPct: 0.05), GapUp45To60));
    }

    [Fact]
    public void OnAGapDownOpen_theGapUpOverrideDoesNotApply()
    {
        Assert.True(Passes(Put(rsi5: 33, gapPct: -0.80), GapUp45To60));
        Assert.False(Passes(Put(rsi5: 50, gapPct: -0.80), GapUp45To60));
    }

    [Fact]
    public void TheGapThreshold_isRespected()
    {
        // 0.20% is below the 0.30% default, so the session is Normal and the live band applies.
        Assert.False(Passes(Put(rsi5: 50, gapPct: 0.20), GapUp45To60));
        Assert.True(Passes(Put(rsi5: 50, gapPct: 0.31), GapUp45To60));
    }

    [Fact]
    public void BlockOnGapUp_refusesTheSideEntirelyOnThoseSessions()
    {
        var block = new EntryRules { OpeningBandAppliesTo = OpeningType.GapUp, OpeningBandBlocks = true };
        var v = Eval(block).Evaluate(Put(rsi5: 33, gapPct: 0.80), TradeSide.Put);
        Assert.False(v.Candidate);
        Assert.Equal("OpeningBand", v.BlockedBy);

        // and leaves other sessions alone
        Assert.True(Passes(Put(rsi5: 33, gapPct: -0.80), block));
    }

    [Fact]
    public void WithNoOverrideConfigured_behaviourIsUnchanged()
    {
        Assert.True(Passes(Put(rsi5: 33, gapPct: 0.80), new EntryRules()));
        Assert.False(Passes(Put(rsi5: 50, gapPct: 0.80), new EntryRules()));
    }
}
