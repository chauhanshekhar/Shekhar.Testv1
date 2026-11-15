using Trading.Backtest.Data;
using Trading.Backtest.Indicators;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The gap-neutral 5m RSI experiment: overnight gaps are removed, nothing else changes.</summary>
public class BacktestGapNeutralRsiTests
{
    private static Bar[] TwoDays(double gap)
    {
        var bars = new List<Bar>();
        var price = 24000.0;
        foreach (var (day, g) in new[] { (BtSupport.Day.AddDays(-1), 0.0), (BtSupport.Day, gap) })
        {
            price += g;                                              // the overnight gap lands on the first bar's open
            for (var i = 0; i < 20; i++)
            {
                var open = price;
                price += (i % 3 == 0 ? -4 : 5);                      // same wiggle on both days
                bars.Add(new Bar(day.AddHours(3).AddMinutes(45 + 5 * i), open, Math.Max(open, price), Math.Min(open, price), price));
            }
        }
        return bars.ToArray();
    }

    [Fact]
    public void RemovingTheGapMakesTheSeriesContinuousAcrossTheDayBoundary()
    {
        var bars = TwoDays(gap: 80);
        var neutral = MarketSeriesBuilder.GapNeutralCloses(bars, 0, bars.Length);

        for (var i = 1; i < bars.Length; i++)
            if (i != 20)                                                                         // index 20 is the first bar of the second day
            Assert.Equal(bars[i].Close - bars[i - 1].Close, neutral[i] - neutral[i - 1], 6);   // bar-to-bar changes inside a day are unchanged
        Assert.Equal(bars[20].Open, neutral[19], 6);                                             // yesterday's last (shifted) close meets today's open
        Assert.Equal(bars[^1].Close, neutral[^1], 6);                                            // the latest bar is never shifted
    }

    [Fact]
    public void ARsiWithTheGapRemovedEqualsTheRsiOfTheSameDaysWithoutAGap()
    {
        var gapped = TwoDays(gap: 80);
        var flat = TwoDays(gap: 0);

        var neutral = IndicatorMath.Rsi(MarketSeriesBuilder.GapNeutralCloses(gapped, 0, gapped.Length));
        var noGap = IndicatorMath.Rsi(flat.Select(b => b.Close).ToArray());

        Assert.Equal(noGap, neutral, 2);
        Assert.NotEqual(noGap, IndicatorMath.Rsi(gapped.Select(b => b.Close).ToArray()));      // the raw series is distorted by the gap
    }

    [Fact]
    public void ASeriesWithoutAnOvernightGapIsReturnedUnchanged()
    {
        var bars = TwoDays(gap: 0);
        var neutral = MarketSeriesBuilder.GapNeutralCloses(bars, 0, bars.Length);
        Assert.Equal(bars.Select(b => b.Close), neutral);
    }
}
