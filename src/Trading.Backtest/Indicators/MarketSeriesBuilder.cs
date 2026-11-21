using Trading.Backtest.Data;

namespace Trading.Backtest.Indicators;

/// <summary>
/// Rebuilds the live indicator inputs for every decision minute from CLOSED bars only (no look-ahead):
///   1m window  = last 200 closed 1m bars (crosses days); EMA20/EMA50/RSI(14) over that window;
///   5m window  = last 100 closed 5m bars; RSI(14) and EMA20/EMA50 over that window;
///   ATR(14)    = Wilder over TODAY's bars inside the 1m window (the last 14 bars of the window while today has fewer than 14);
///   day change = (Ltp - open of the first of today's bars inside the 1m window) / that open x 100 (the live engine's behaviour:
///                the "first bar of today" moves forward once a session passes 200 bars); Ltp = close of the last closed 1m bar.
/// A 1m bar is closed at <c>now</c> when its open + 1 minute &lt;= now; a 5m bar when its open + 5 minutes &lt;= now.
/// The decision minute is the minute boundary: a bar that opens at that boundary is NOT visible.
/// </summary>
public static class MarketSeriesBuilder
{
    public const int Window1m = 200;
    public const int Window5m = 100;

    /// <param name="gapNeutralRsi5">
    /// Experiment, not production behaviour: removes every overnight gap (today's first 5m open minus the previous 5m close) from the
    /// 5m series before the 5m RSI is computed, so a gap open does not push the RSI out of the entry band for the next ~70 minutes.
    /// </param>
    public static List<MarketPoint> Build(string symbol, IReadOnlyList<Bar> bars1m, IReadOnlyList<Bar> bars5m, bool gapNeutralRsi5 = false)
    {
        var b1 = bars1m.OrderBy(b => b.OpenUtc).ToArray();
        var b5 = bars5m.OrderBy(b => b.OpenUtc).ToArray();
        var t1 = b1.Select(b => b.OpenUtc).ToArray();
        var t5 = b5.Select(b => b.OpenUtc).ToArray();
        var c1 = b1.Select(b => b.Close).ToArray();
        var c5 = b5.Select(b => b.Close).ToArray();

        var points = new List<MarketPoint>();
        foreach (var date in t1.Select(t => t.Date).Distinct())
        {
            var dayStart = LowerBound(t1, date);
            var i0 = LowerBound(t1, date + SessionClock.EntryStartUtc);
            var i1 = LowerBound(t1, date + SessionClock.EntryEndUtc);

            for (var i = i0; i < i1; i++)
            {
                var point = At(symbol, t1[i], b1, t1, c1, b5, t5, c5, dayStart, gapNeutralRsi5);
                if (point is not null)
                    points.Add(point);
            }
        }
        return points;
    }

    /// <summary>The indicator state visible at <paramref name="nowUtc"/>, or null when there is not enough closed history.</summary>
    public static MarketPoint? At(
        string symbol, DateTime nowUtc,
        Bar[] b1, DateTime[] t1, double[] c1, Bar[] b5, DateTime[] t5, double[] c5, int dayStart, bool gapNeutralRsi5 = false)
    {
        // number of 1m bars already closed at nowUtc (open <= now - 1 minute)
        var j = UpperBound(t1, nowUtc - TimeSpan.FromMinutes(1));
        if (j <= dayStart || j < 14)
            return null;

        var winStart = Math.Max(0, j - Window1m);
        var closes = c1.AsSpan(winStart, j - winStart);
        var todayStartInWindow = Math.Max(dayStart, winStart);
        var todayCount = j - todayStartInWindow;

        var atrStart = todayCount >= 14 ? todayStartInWindow : j - 14;
        var atr = IndicatorMath.Atr(b1.AsSpan(atrStart, j - atrStart));

        var e20 = IndicatorMath.Ema(closes, 20);
        var e50 = IndicatorMath.Ema(closes, 50);
        if (e20 == 0 || e50 == 0 || atr <= 0)
            return null;

        var ltp = c1[j - 1];
        // Production reads "today" from the 1m window, so once a session has more than 200 bars the first bar it sees moves forward.
        // The stored PriceChangePct drifts the same way (verified against IndicatorSnapshots), and the replay copies it on purpose.
        var dayOpen = b1[todayStartInWindow].Open;

        double low = double.MaxValue, high = double.MinValue;
        for (var q = todayStartInWindow; q < j; q++)
        {
            if (b1[q].Low < low) low = b1[q].Low;
            if (b1[q].High > high) high = b1[q].High;
        }

        // 5m bars closed at nowUtc: open <= now - 5 minutes
        var k = UpperBound(t5, nowUtc - TimeSpan.FromMinutes(5));
        Console.WriteLine(
    $"RSI5 DEBUG | {symbol} | {nowUtc:yyyy-MM-dd HH:mm} UTC | " +
    $"k={k} | First5m={t5.FirstOrDefault():yyyy-MM-dd HH:mm} | " +
    $"LastClosed5m={(k > 0 ? t5[k - 1].ToString("yyyy-MM-dd HH:mm") : "NONE")}");
        double? rsi5 = null;
        double e20_5 = 0, e50_5 = 0;
        double atr5 = 0;
        if (k > 14)
        {
            var w5 = c5.AsSpan(Math.Max(0, k - Window5m), k - Math.Max(0, k - Window5m));
            rsi5 = IndicatorMath.Rsi(gapNeutralRsi5 ? GapNeutralCloses(b5, Math.Max(0, k - Window5m), k) : w5);
            e20_5 = IndicatorMath.Ema(w5, 20);
            e50_5 = IndicatorMath.Ema(w5, 50);
            atr5 = IndicatorMath.Atr(b5.AsSpan(Math.Max(0, k - Window5m), k - Math.Max(0, k - Window5m)));
        }
        Console.WriteLine(
    $"RSI5 RESULT | {symbol} | {nowUtc:yyyy-MM-dd HH:mm} UTC | " +
    $"Rsi5m={rsi5?.ToString("F2") ?? "NULL"}");

        return new MarketPoint
        {
            Symbol = symbol,
            TimeUtc = nowUtc,
            NextBarIndex = j,
            Ltp = ltp,
            Ema20 = e20,
            Ema50 = e50,
            Ema9 = IndicatorMath.Ema(closes, 9),
            Ema21 = IndicatorMath.Ema(closes, 21),
            Rsi1m = IndicatorMath.Rsi(closes),
            Rsi5m = rsi5,
            Atr = atr,
            AtrPct = ltp > 0 ? atr / ltp * 100.0 : 0.0,
            DayChangePct = dayOpen > 0 ? (ltp - dayOpen) / dayOpen * 100.0 : 0.0,
            DayOpen = dayOpen,
            DayLow = low,
            DayHigh = high,
            Ema20_5m = e20_5,
            Ema50_5m = e50_5,
            Atr5m = atr5,
            LastClosedBar = b1[j - 1],
            Prev5mHigh = k > 0 ? b5[k - 1].High : 0.0,
            Prev5mLow = k > 0 ? b5[k - 1].Low : 0.0,
            SessionOpen = b1[dayStart].Open,
            SessionLow = MinLow(b1, dayStart, j),
            MinLow10 = MinLow(b1, Math.Max(0, j - 10), j),
            MaxHigh10Prior = MaxHigh(b1, Math.Max(0, j - 11), j - 1),
            OpenGapPct = dayStart > 0 && c1[dayStart - 1] > 0 ? (b1[dayStart].Open - c1[dayStart - 1]) / c1[dayStart - 1] * 100.0 : null,
        };
    }

    /// <summary>
    /// Closes of b5[from..to) with each overnight gap taken out: every bar before a day boundary is shifted by the gap that opened the
    /// following day (open of the day's first bar minus the previous bar's close), so the series is continuous across days.
    /// </summary>
    public static double[] GapNeutralCloses(Bar[] b5, int from, int to)
    {
        var closes = new double[to - from];
        var shift = 0.0;
        for (var i = to - 1; i >= from; i--)
        {
            if (i < to - 1 && b5[i + 1].OpenUtc.Date != b5[i].OpenUtc.Date)
                shift += b5[i + 1].Open - b5[i].Close;
            closes[i - from] = b5[i].Close + shift;
        }
        return closes;
    }

    /// <summary>Attaches the last snapshot at or before each point's IST time (within <paramref name="maxAge"/>); never a later one.</summary>
    public static List<MarketPoint> AttachSnapshots(IReadOnlyList<MarketPoint> points, IReadOnlyList<SnapshotRow> snapshots, TimeSpan maxAge)
    {
        var ordered = snapshots.OrderBy(s => s.SnapshotTime).ToArray();
        var times = ordered.Select(s => s.SnapshotTime).ToArray();
        var result = new List<MarketPoint>(points.Count);

        foreach (var p in points)
        {
            var idx = UpperBound(times, p.TimeIst) - 1;
            if (idx >= 0 && p.TimeIst - times[idx] <= maxAge)
            {
                var s = ordered[idx];
                result.Add(p with
                {
                    Pcr = s.Pcr > 0 ? (double)s.Pcr : null,
                    MarketState = s.MarketState,
                    TrendDirection = s.TrendDirection,
                    StrategySelected = s.StrategySelected,
                    StoredMarketRegime = s.MarketRegime,
                });
            }
            else
            {
                result.Add(p);
            }
        }
        return result;
    }

    private static double MinLow(Bar[] b, int from, int to)
    {
        var v = double.MaxValue;
        for (var i = from; i < to; i++) v = Math.Min(v, b[i].Low);
        return v;
    }

    private static double MaxHigh(Bar[] b, int from, int to)
    {
        var v = 0.0;
        for (var i = from; i < to; i++) v = Math.Max(v, b[i].High);
        return v;
    }

    /// <summary>Index of the first element &gt;= value.</summary>
    internal static int LowerBound(DateTime[] a, DateTime value)
    {
        int lo = 0, hi = a.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (a[mid] < value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    /// <summary>Count of elements &lt;= value (index of the first element &gt; value).</summary>
    internal static int UpperBound(DateTime[] a, DateTime value)
    {
        int lo = 0, hi = a.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (a[mid] <= value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
