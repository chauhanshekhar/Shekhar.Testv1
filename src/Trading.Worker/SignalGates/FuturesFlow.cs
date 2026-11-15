using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Volume and VWAP from the near-month index future's 1m bars ({SYMBOL}_FUT_1m, see GrowwFutures). The index itself
/// publishes no volume. Pure functions so the arithmetic is testable.
/// </summary>
internal static class FuturesFlow
{
    /// <summary>Session open in UTC (09:15 IST) - the 5m buckets are aligned to it. Candle.CandleTime is UTC.</summary>
    private static readonly TimeSpan SessionOpenUtc = new(3, 45, 0);

    internal readonly record struct Flow(decimal Vwap, decimal CurrentVolume, decimal AvgVolume20, bool CumulativeSource);

    /// <summary>
    /// Per-bar volumes for one day's bars in time order. Groww's futures candles carry the day's RUNNING total:
    /// BANKNIFTY26OCTFUT 2026-10-08 09:15-09:21 = 17760, 21480, 25680, 28200, 32460, 35790, 39120 (every step a
    /// multiple of the 30 lot). A series that never falls is differenced (the first bar keeps its own value); one that
    /// ever falls is already per-bar and is returned unchanged.
    /// </summary>
    internal static (long[] Volumes, bool Cumulative) PerBarVolumes(IReadOnlyList<long> raw)
    {
        for (var i = 1; i < raw.Count; i++)
            if (raw[i] < raw[i - 1])
                return (raw.ToArray(), false);

        var perBar = new long[raw.Count];
        for (var i = 0; i < raw.Count; i++)
            perBar[i] = i == 0 ? raw[0] : raw[i] - raw[i - 1];
        return (perBar, true);
    }

    /// <summary>
    /// The future's session VWAP (typical price weighted by per-bar volume), shifted by the current basis
    /// (future close - index LTP) into index points, so it is directly comparable with the index LTP:
    /// index LTP > result exactly when the future's close is above its own VWAP. Null when there is no volume.
    /// </summary>
    internal static decimal? VwapInIndexPoints(IReadOnlyList<Candle> bars, IReadOnlyList<long> volumes, decimal indexLtp)
    {
        decimal pv = 0m, v = 0m;
        for (var i = 0; i < bars.Count; i++)
        {
            pv += (bars[i].High + bars[i].Low + bars[i].Close) / 3m * volumes[i];
            v += volumes[i];
        }
        if (v <= 0 || bars.Count == 0 || indexLtp <= 0)
            return null;

        var basis = bars[^1].Close - indexLtp;
        return pv / v - basis;
    }

    /// <summary>
    /// 5m volumes from the 1m bars, buckets aligned to 09:15 IST. Only complete buckets (all 5 bars present) count.
    /// Current = the last complete bucket; average = up to 20 complete buckets before it. Null with fewer than 2.
    /// </summary>
    internal static (decimal Current, decimal Avg20)? FiveMinuteVolume(IReadOnlyList<Candle> bars, IReadOnlyList<long> volumes)
    {
        var buckets = new SortedDictionary<long, (int Bars, long Volume)>();
        for (var i = 0; i < bars.Count; i++)
        {
            var minutes = (long)(bars[i].CandleTime.TimeOfDay - SessionOpenUtc).TotalMinutes;
            if (minutes < 0) continue;
            var key = bars[i].CandleTime.Date.Ticks / TimeSpan.TicksPerMinute + minutes / 5;
            buckets.TryGetValue(key, out var b);
            buckets[key] = (b.Bars + 1, b.Volume + volumes[i]);
        }

        var complete = buckets.Values.Where(b => b.Bars == 5).Select(b => (decimal)b.Volume).ToList();
        if (complete.Count < 2)
            return null;

        var avg = complete.SkipLast(1).TakeLast(20).Average();
        return (complete[^1], avg > 0 ? avg : 1m);
    }

    /// <summary>Everything above for today's closed future bars (time order). Null when there are no usable bars.</summary>
    internal static Flow? From(IReadOnlyList<Candle> todayBars, decimal indexLtp)
    {
        if (todayBars.Count == 0)
            return null;

        // Missing minutes are NOT a quality problem: Groww omits minutes with no trades (thin SENSEX future), and the
        // running-total differencing still gives the right volume across the gap (checked 2026-10-12).
        var (volumes, cumulative) = PerBarVolumes(todayBars.Select(b => b.Volume).ToList());

        // Groww futures volume is a running total. A series that falls is a mixed or corrupt feed
        // (seen on SENSEX 2026-10-09), not per-bar data - no VWAP is better than a wrongly weighted one.
        if (!cumulative)
            return null;

        var vwap = VwapInIndexPoints(todayBars, volumes, indexLtp);
        if (vwap is null)
            return null;

        var five = FiveMinuteVolume(todayBars, volumes);
        return new Flow(vwap.Value, five?.Current ?? 0m, five?.Avg20 ?? 1m, cumulative);
    }
}
