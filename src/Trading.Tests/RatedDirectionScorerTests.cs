using Trading.SignalEngine.Strategies;
using Trading.Tests.Fakes;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Rated direction (no RSI). The contract: off with no settings rows; each component adds its documented points; the winner
/// needs both the minimum score and the margin; RSI settings play no part.
/// </summary>
public class RatedDirectionScorerTests
{
    private const string Sym = "NIFTY";

    private static FakeStrategySettingsService Live() => new FakeStrategySettingsService()
        .Set(RatedDirectionScorer.EnableKey, 1m)
        .Set("EmaGapMinPoints", 10m)
        .Set("CallPcrMinimum", 0.98m).Set("CallPcrMaximum", 1.50m)
        .Set("PutPcrMaximum", 1.40m)
        .Set("MinAtrPercent", 0.03m);

    // A full-marks CALL: aligned, gap 25 (>= 2 x 10), day +0.6%, bullish bar, PCR 1.1, ATR 0.05.
    private static RatedDirectionInput Bull(
        decimal ltp = 25100m, decimal ema20 = 25085m, decimal ema50 = 25060m, decimal? pcr = 1.1m,
        decimal atrPct = 0.05m, decimal dayChg = 0.6m, bool bullish = true, bool bearish = false) =>
        new(ltp, ema20, ema50, pcr, atrPct, dayChg, bullish, bearish);

    [Fact]
    public void NoSettingsRows_IsDisabled() =>
        Assert.False(new RatedDirectionScorer(new FakeStrategySettingsService()).IsEnabled(Sym));

    [Fact]
    public void FullMarksCall_Scores100_AndWins()
    {
        var s = new RatedDirectionScorer(Live());
        var d = s.Decide(Sym, Bull());
        Assert.Equal(100m, d.CallScore);
        Assert.Equal("CALL", d.Winner);
        Assert.True(d.PutScore < d.CallScore);
    }

    [Fact]
    public void MirroredInput_PutWins()
    {
        var d = new RatedDirectionScorer(Live()).Decide(Sym,
            new RatedDirectionInput(25000m, 25015m, 25040m, 1.1m, 0.05m, -0.6m, false, true));
        Assert.Equal(100m, d.PutScore);
        Assert.Equal("PUT", d.Winner);
    }

    [Theory]
    [InlineData("priceNotAligned", 85)]   // trend 30 -> 15
    [InlineData("smallGap", 95)]          // gap 15 -> 10
    [InlineData("noGap", 85)]             // gap 15 -> 0
    [InlineData("weakDay", 94)]           // day 12 -> 6
    [InlineData("noBar", 92)]             // bar 8 -> 0
    [InlineData("pcrOut", 80)]            // pcr 20 -> 0
    [InlineData("noPcr", 80)]
    [InlineData("lowAtr", 85)]            // atr 15 -> 0
    public void EachComponent_HasItsWeight(string which, int expected)
    {
        var input = which switch
        {
            "priceNotAligned" => Bull(ltp: 25080m),
            "smallGap" => Bull(ema20: 25075m),
            "noGap" => Bull(ema20: 25065m),
            "weakDay" => Bull(dayChg: 0.3m),
            "noBar" => Bull(bullish: false),
            "pcrOut" => Bull(pcr: 1.6m),
            "noPcr" => Bull(pcr: null),
            _ => Bull(atrPct: 0.02m),
        };
        Assert.Equal(expected, new RatedDirectionScorer(Live()).Score(Sym, true, input));
    }

    [Fact]
    public void BelowMinScore_NoWinner()
    {
        var d = new RatedDirectionScorer(Live()).Decide(Sym, Bull(pcr: 1.6m, atrPct: 0.02m), minScore: 70m, minMargin: 0m);
        Assert.Equal(65m, d.CallScore);
        Assert.Null(d.Winner);
    }

    [Fact]
    public void MarginTooSmall_NoWinner()
    {
        // EMAs flat: neither side trend-aligned; both earn ATR + PCR, CALL adds momentum -> small margin.
        var d = new RatedDirectionScorer(Live()).Decide(Sym, Bull(ema20: 25060m, dayChg: 0.1m, bullish: true, pcr: 1.1m),
            minScore: 0m, minMargin: 30m);
        Assert.Null(d.Winner);
        Assert.Contains("margin", d.Reason);
    }

    [Fact]
    public void RsiSettings_AreIgnored()
    {
        var s = Live().Set("CallRsiFloor", 45m).Set("CallRsiCeiling", 70m);
        Assert.Equal(100m, new RatedDirectionScorer(s).Score(Sym, true, Bull()));
    }
}
