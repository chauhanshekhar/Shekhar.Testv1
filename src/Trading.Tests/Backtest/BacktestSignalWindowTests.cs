using Trading.Backtest.Data;
using Trading.Backtest.Indicators;
using Trading.Domain;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// The replay builds decision minutes only inside the live signal window: SignalWorker generates signals only while
/// MarketHours.IsSignalWindow() is true (SignalOpen 09:25 IST to SignalClose 15:10 IST, both inclusive), so a backtest that
/// evaluates 09:15-09:24 or 15:11-15:14 can produce signals production never could.
/// The first decision is 09:30, not 09:25: ATR(14) needs 15 bars, and live MarketSnapshot falls back to the last 14 bars while
/// today has fewer than 14, so live ATR is 0 (no tradable signal) until today's 15th bar has closed. The replay copies that.
/// </summary>
public class BacktestSignalWindowTests
{
    /// <summary>Two full sessions (previous day + BtSupport.Day) of 1m bars from 09:15 to 15:29 IST, plus their 5m bars.</summary>
    private static (Bar[] B1, Bar[] B5) TwoSessions()
    {
        var rnd = new Random(11);
        var bars = new List<Bar>();
        var price = 24000.0;
        foreach (var day in new[] { BtSupport.Day.AddDays(-1), BtSupport.Day })
        {
            for (var i = 0; i < 375; i++)
            {
                var open = price;
                price += (rnd.NextDouble() - 0.5) * 12;
                bars.Add(new Bar(day.AddHours(3).AddMinutes(45 + i), open, Math.Max(open, price) + 1, Math.Min(open, price) - 1, price));
            }
        }

        var five = new List<Bar>();
        for (var i = 0; i + 5 <= bars.Count; i += 5)
        {
            var g = bars.Skip(i).Take(5).ToArray();
            five.Add(new Bar(g[0].OpenUtc, g[0].Open, g.Max(x => x.High), g.Min(x => x.Low), g[4].Close));
        }
        return (bars.ToArray(), five.ToArray());
    }

    [Fact]
    public void DecisionMinutesStartAtSignalOpenAndEndAtSignalClose()
    {
        var (b1, b5) = TwoSessions();

        var today = MarketSeriesBuilder.Build("NIFTY", b1, b5)
            .Where(p => p.TimeUtc.Date == BtSupport.Day)
            .OrderBy(p => p.TimeUtc)
            .ToList();

        Assert.NotEmpty(today);
        var firstWithAtr = MarketHours.SessionOpen + TimeSpan.FromMinutes(15);   // 15 closed bars of today: 09:30 IST
        var expectedFirst = firstWithAtr > MarketHours.SignalOpen ? firstWithAtr : MarketHours.SignalOpen;
        Assert.Equal(expectedFirst, today[0].TimeIst.TimeOfDay);
        Assert.Equal(MarketHours.SignalClose, today[^1].TimeIst.TimeOfDay);
    }

    [Fact]
    public void NoDecisionMinuteFallsOutsideTheLiveSignalWindow()
    {
        var (b1, b5) = TwoSessions();

        var points = MarketSeriesBuilder.Build("NIFTY", b1, b5);

        Assert.NotEmpty(points);
        Assert.All(points, p => Assert.InRange(p.TimeIst.TimeOfDay, MarketHours.SignalOpen, MarketHours.SignalClose));
    }
}
