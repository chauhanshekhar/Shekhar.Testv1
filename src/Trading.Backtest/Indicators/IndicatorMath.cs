using Trading.Backtest.Data;

namespace Trading.Backtest.Indicators;

/// <summary>
/// Indicator maths ported from the validated research rebuild (docs/research/2026-10-12-long-history-price-conditions):
/// the rebuilt RSI/EMA/ATR matched the stored IndicatorSnapshots with a median absolute difference of 0.00.
/// EMA seeds with the SMA of the first <c>period</c> values; RSI and ATR are Wilder-smoothed.
/// </summary>
public static class IndicatorMath
{
    public static double Ema(ReadOnlySpan<double> values, int period)
    {
        if (values.Length < period)
            return 0.0;

        var e = 0.0;
        for (var i = 0; i < period; i++) e += values[i];
        e /= period;

        var k = 2.0 / (period + 1);
        for (var i = period; i < values.Length; i++)
            e += (values[i] - e) * k;

        return Math.Round(e, 2, MidpointRounding.ToEven);
    }

    public static double Rsi(ReadOnlySpan<double> values, int period = 14)
    {
        if (values.Length <= period)
            return 50.0;

        double ag = 0, al = 0;
        for (var i = 1; i <= period; i++)
        {
            var c = values[i] - values[i - 1];
            if (c > 0) ag += c; else al -= c;
        }
        ag /= period; al /= period;

        for (var i = period + 1; i < values.Length; i++)
        {
            var c = values[i] - values[i - 1];
            ag = (ag * (period - 1) + (c > 0 ? c : 0)) / period;
            al = (al * (period - 1) + (c < 0 ? -c : 0)) / period;
        }

        if (ag == 0 && al == 0) return 50.0;
        if (al == 0) return 100.0;
        if (ag == 0) return 0.0;
        return Math.Round(100 - 100 / (1 + ag / al), 2, MidpointRounding.ToEven);
    }

    /// <summary>Wilder ATR over the given bars; 0 when fewer than period + 1 bars exist.</summary>
    public static double Atr(ReadOnlySpan<Bar> bars, int period = 14)
    {
        if (bars.Length < period + 1)
            return 0.0;

        double v = 0;
        for (var i = 1; i <= period; i++) v += TrueRange(bars, i);
        v /= period;

        for (var i = period + 1; i < bars.Length; i++)
            v = (v * (period - 1) + TrueRange(bars, i)) / period;

        return v;
    }

    private static double TrueRange(ReadOnlySpan<Bar> bars, int i) =>
        Math.Max(bars[i].High - bars[i].Low,
            Math.Max(Math.Abs(bars[i].High - bars[i - 1].Close), Math.Abs(bars[i].Low - bars[i - 1].Close)));
}
