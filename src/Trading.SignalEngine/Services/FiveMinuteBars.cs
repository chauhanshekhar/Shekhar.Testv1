using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

/// <summary>
/// Pure 5-minute bar measurements shared by CloseBreakoutGate, EfficiencyRatioGate, StopNoiseGate and the
/// 5m-ATR stop in StrategyManager. Every method expects a chronological, CLOSED-ONLY series
/// (MarketSnapshot.Candles5m already excludes the forming bar) and returns null / 0 when there is not
/// enough data, so callers decide explicitly whether to fail open or closed.
/// </summary>
public static class FiveMinuteBars
{
    /// <summary>Wilder ATR over the series (same math as AtrService). 0 when fewer than period + 1 bars.</summary>
    public static decimal Atr(List<Candle> candles5m, int period = 14) =>
        new AtrService().Calculate(candles5m, period);

    /// <summary>Average High-Low range of the last <paramref name="bars"/> bars, or null when fewer exist.</summary>
    public static decimal? AverageRange(IReadOnlyList<Candle> candles5m, int bars)
    {
        if (bars <= 0 || candles5m.Count < bars) return null;

        decimal sum = 0m;
        for (var i = candles5m.Count - bars; i < candles5m.Count; i++)
            sum += candles5m[i].High - candles5m[i].Low;

        return sum / bars;
    }

    /// <summary>
    /// Kaufman Efficiency Ratio over the last <paramref name="bars"/> closes:
    /// |Close[last] - Close[last - bars]| / sum(|Close[i] - Close[i-1]|).
    /// 1 = straight-line move, near 0 = back-and-forth chop. Null when fewer than bars + 1 bars exist or the
    /// path length is zero (flat).
    /// </summary>
    public static decimal? EfficiencyRatio(IReadOnlyList<Candle> candles5m, int bars)
    {
        if (bars <= 0 || candles5m.Count < bars + 1) return null;

        var last = candles5m.Count - 1;
        var net = Math.Abs(candles5m[last].Close - candles5m[last - bars].Close);

        decimal path = 0m;
        for (var i = last - bars + 1; i <= last; i++)
            path += Math.Abs(candles5m[i].Close - candles5m[i - 1].Close);

        return path > 0m ? net / path : null;
    }

    /// <summary>
    /// Did the LAST closed 5m bar CLOSE beyond the extreme of the <paramref name="lookback"/> bars before it?
    /// CALL: Close &gt; highest High. PUT: Close &lt; lowest Low. Null when fewer than lookback + 1 bars exist.
    /// Returns the breakout level alongside the verdict for logging.
    /// </summary>
    public static (bool Broke, decimal Level)? CloseBreakout(IReadOnlyList<Candle> candles5m, int lookback, bool call)
    {
        if (lookback <= 0 || candles5m.Count < lookback + 1) return null;

        var last = candles5m[^1];
        var level = call ? decimal.MinValue : decimal.MaxValue;

        for (var i = candles5m.Count - 1 - lookback; i < candles5m.Count - 1; i++)
            level = call ? Math.Max(level, candles5m[i].High) : Math.Min(level, candles5m[i].Low);

        return (call ? last.Close > level : last.Close < level, level);
    }
}
