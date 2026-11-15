using Trading.Backtest.Data;
using Trading.Backtest.Indicators;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>No future information: a decision minute sees only bars that were already closed at that minute.</summary>
public class BacktestLookAheadTests
{
    private sealed class Series
    {
        public Bar[] B1 = [];
        public DateTime[] T1 = [];
        public double[] C1 = [];
        public Bar[] B5 = [];
        public DateTime[] T5 = [];
        public double[] C5 = [];
        public int DayStart;

        public static Series Build(Func<Bar, Bar>? mutate = null)
        {
            var rnd = new Random(7);
            var bars = new List<Bar>();
            var price = 24000.0;
            foreach (var day in new[] { BtSupport.Day.AddDays(-1), BtSupport.Day })
            {
                for (var i = 0; i < 375; i++)
                {
                    var open = price;
                    price += (rnd.NextDouble() - 0.5) * 12;
                    var b = new Bar(day.AddHours(3).AddMinutes(45 + i), open, Math.Max(open, price) + 1, Math.Min(open, price) - 1, price);
                    bars.Add(mutate?.Invoke(b) ?? b);
                }
            }

            var s = new Series { B1 = bars.ToArray() };
            s.T1 = s.B1.Select(b => b.OpenUtc).ToArray();
            s.C1 = s.B1.Select(b => b.Close).ToArray();

            var five = new List<Bar>();
            for (var i = 0; i + 5 <= s.B1.Length; i += 5)
            {
                var g = s.B1.Skip(i).Take(5).ToArray();
                if ((g[4].OpenUtc - g[0].OpenUtc).TotalMinutes != 4) continue;
                five.Add(new Bar(g[0].OpenUtc, g[0].Open, g.Max(x => x.High), g.Min(x => x.Low), g[4].Close));
            }
            s.B5 = five.ToArray();
            s.T5 = s.B5.Select(b => b.OpenUtc).ToArray();
            s.C5 = s.B5.Select(b => b.Close).ToArray();
            s.DayStart = Array.FindIndex(s.T1, t => t.Date == BtSupport.Day);
            return s;
        }

        public MarketPoint? At(DateTime now) =>
            MarketSeriesBuilder.At("NIFTY", now, B1, T1, C1, B5, T5, C5, DayStart);
    }

    private static readonly DateTime Now = BtSupport.At(5, 0);       // 10:30 IST on the second day

    [Fact]
    public void BarsOpeningAtOrAfterTheDecisionMinuteCannotChangeThePoint()
    {
        var clean = Series.Build().At(Now)!;
        var future = Series.Build(b => b.OpenUtc >= Now ? b with { Open = 1, High = 99999, Low = 1, Close = 55555 } : b).At(Now)!;

        Assert.Equal(clean.Ltp, future.Ltp);
        Assert.Equal(clean.Ema20, future.Ema20);
        Assert.Equal(clean.Ema50, future.Ema50);
        Assert.Equal(clean.Rsi1m, future.Rsi1m);
        Assert.Equal(clean.Rsi5m, future.Rsi5m);
        Assert.Equal(clean.Atr, future.Atr);
        Assert.Equal(clean.DayChangePct, future.DayChangePct);
        Assert.Equal(clean.DayHigh, future.DayHigh);
        Assert.Equal(clean.DayLow, future.DayLow);
        Assert.Equal(clean.Ema20_5m, future.Ema20_5m);
        Assert.Equal(clean.Ema50_5m, future.Ema50_5m);
    }

    [Fact]
    public void TheBarOpeningAtTheDecisionMinuteIsStillFormingAndIsNotUsed()
    {
        var clean = Series.Build().At(Now)!;
        var forming = Series.Build(b => b.OpenUtc == Now ? b with { Close = b.Close + 500, High = b.High + 500 } : b).At(Now)!;
        Assert.Equal(clean.Ltp, forming.Ltp);
        Assert.Equal(clean.Rsi1m, forming.Rsi1m);
    }

    [Fact]
    public void TheBarClosedAtTheDecisionMinuteIsUsedAsTheLastPrice()
    {
        var clean = Series.Build().At(Now)!;
        var closed = Series.Build(b => b.OpenUtc == Now.AddMinutes(-1) ? b with { Close = b.Close + 50, High = b.High + 50 } : b).At(Now)!;
        Assert.Equal(clean.Ltp + 50, closed.Ltp);
        Assert.Equal(clean.LastClosedBar.OpenUtc, Now.AddMinutes(-1));
        Assert.NotEqual(clean.Ema20, closed.Ema20);
    }

    [Fact]
    public void FiveMinuteIndicatorsUseOnlyCompleted5mBars()
    {
        // the 5m bar that opened 3 minutes before the decision is still forming; the one that opened 5 minutes before is complete
        var clean = Series.Build().At(Now)!;

        var mutatedForming = Series.Build(b => b.OpenUtc >= FiveMinuteOpen(Now) ? b with { Close = b.Close + 400 } : b);
        var p = mutatedForming.At(Now)!;
        Assert.Equal(clean.Rsi5m, p.Rsi5m);
        Assert.Equal(clean.Ema20_5m, p.Ema20_5m);

        var lastClosed5m = FiveMinuteOpen(Now).AddMinutes(-5);
        var changed = Series.Build(b => b.OpenUtc >= lastClosed5m && b.OpenUtc < FiveMinuteOpen(Now) ? b with { Close = b.Close + 400, High = b.High + 400 } : b).At(Now)!;
        Assert.NotEqual(clean.Rsi5m, changed.Rsi5m);
    }

    private static DateTime FiveMinuteOpen(DateTime t) => t.AddMinutes(-(t.Minute % 5)).AddSeconds(-t.Second);

    [Fact]
    public void SnapshotJoinNeverUsesALaterSnapshotAndIgnoresStaleOnes()
    {
        var p = new MarketPoint
        {
            Symbol = "NIFTY", TimeUtc = Now, NextBarIndex = 0, Ltp = 1, Ema20 = 1, Ema50 = 1, Rsi1m = 50, Rsi5m = 50, Atr = 1, AtrPct = 0.1,
            DayChangePct = 0, DayOpen = 1, DayLow = 1, DayHigh = 1, Ema20_5m = 0, Ema50_5m = 0, LastClosedBar = new Bar(Now.AddMinutes(-1), 1, 1, 1, 1),
        };
        var ist = p.TimeIst;
        var earlier = new SnapshotRow { Symbol = "NIFTY", SnapshotTime = ist.AddSeconds(-30), Pcr = 0.9m, MarketState = "Trend" };
        var later = new SnapshotRow { Symbol = "NIFTY", SnapshotTime = ist.AddSeconds(1), Pcr = 2.0m, MarketState = "Breakdown" };
        var stale = new SnapshotRow { Symbol = "NIFTY", SnapshotTime = ist.AddMinutes(-20), Pcr = 1.5m };

        var joined = MarketSeriesBuilder.AttachSnapshots([p], [later, earlier], TimeSpan.FromMinutes(3)).Single();
        Assert.Equal(0.9, joined.Pcr);
        Assert.Equal("Trend", joined.MarketState);

        Assert.Null(MarketSeriesBuilder.AttachSnapshots([p], [later], TimeSpan.FromMinutes(3)).Single().Pcr);   // only a future snapshot exists
        Assert.Null(MarketSeriesBuilder.AttachSnapshots([p], [stale], TimeSpan.FromMinutes(3)).Single().Pcr);   // too old to be the current value
    }

    [Fact]
    public void NotEnoughClosedHistoryGivesNoPoint()
    {
        var s = Series.Build();
        Assert.Null(MarketSeriesBuilder.At("NIFTY", s.T1[20].AddMinutes(1), s.B1, s.T1, s.C1, s.B5, s.T5, s.C5, 0));
    }
}
