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

    /// <summary>A CALL candidate at the given 5m RSI on a session that opened with the given gap. (PUT has no RSI band, so the
    /// opening-band override only applies to CALL.) Live CALL band is 45-65.</summary>
    private static MarketPoint Put(double rsi5, double gapPct) =>
        BtSupport.CallPoint("NIFTY", T, rsi5: rsi5) with { OpenGapPct = gapPct };

    private static readonly EntryRules GapUp70To80 = new()
    {
        OpeningBandAppliesTo = OpeningType.GapUp,
        OpeningBandRsiFloor = 70m,
        OpeningBandRsiCeiling = 80m,
    };

    private static bool Passes(MarketPoint p, EntryRules rules) =>
        Eval(rules).Evaluate(p, TradeSide.Call).Candidate;

    [Fact]
    public void OnAGapUpSession_theOverrideBandApplies_andTheLiveBandDoesNot()
    {
        // RSI 75 is outside the live CALL band (45-65) but inside the gap-up override (70-80).
        Assert.False(Passes(Put(rsi5: 75, gapPct: 0.80), new EntryRules()));
        Assert.True(Passes(Put(rsi5: 75, gapPct: 0.80), GapUp70To80));

        // RSI 55 is inside the live band but outside the override, so the override must reject it.
        Assert.True(Passes(Put(rsi5: 55, gapPct: 0.80), new EntryRules()));
        Assert.False(Passes(Put(rsi5: 55, gapPct: 0.80), GapUp70To80));
    }

    [Fact]
    public void OnANormalOpen_theLiveBandStillApplies()
    {
        Assert.True(Passes(Put(rsi5: 55, gapPct: 0.05), GapUp70To80));
        Assert.False(Passes(Put(rsi5: 75, gapPct: 0.05), GapUp70To80));
    }

    [Fact]
    public void OnAGapDownOpen_theGapUpOverrideDoesNotApply()
    {
        Assert.True(Passes(Put(rsi5: 55, gapPct: -0.80), GapUp70To80));
        Assert.False(Passes(Put(rsi5: 75, gapPct: -0.80), GapUp70To80));
    }

    [Fact]
    public void TheGapThreshold_isRespected()
    {
        // 0.20% is below the 0.30% default, so the session is Normal and the live band applies.
        Assert.False(Passes(Put(rsi5: 75, gapPct: 0.20), GapUp70To80));
        Assert.True(Passes(Put(rsi5: 75, gapPct: 0.31), GapUp70To80));
    }

    [Fact]
    public void BlockOnGapUp_refusesTheSideEntirelyOnThoseSessions()
    {
        var block = new EntryRules { OpeningBandAppliesTo = OpeningType.GapUp, OpeningBandBlocks = true };
        var v = Eval(block).Evaluate(Put(rsi5: 55, gapPct: 0.80), TradeSide.Call);
        Assert.False(v.Candidate);
        Assert.Equal("OpeningBand", v.BlockedBy);

        // and leaves other sessions alone
        Assert.True(Passes(Put(rsi5: 55, gapPct: -0.80), block));
    }

    [Fact]
    public void WithNoOverrideConfigured_behaviourIsUnchanged()
    {
        Assert.True(Passes(Put(rsi5: 55, gapPct: 0.80), new EntryRules()));
        Assert.False(Passes(Put(rsi5: 75, gapPct: 0.80), new EntryRules()));
    }
}
