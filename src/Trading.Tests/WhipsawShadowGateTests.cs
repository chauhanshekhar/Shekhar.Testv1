using Trading.Domain.Entities;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>WhipsawShadowGate scoring (log-only gate; it never blocks).</summary>
public class WhipsawShadowGateTests
{
    private static Candle C(decimal open, decimal close) =>
        new() { Symbol = "NIFTY_1m", CandleTime = DateTime.UtcNow, Open = open, High = Math.Max(open, close), Low = Math.Min(open, close), Close = close };

    private static List<Candle> Closes(params decimal[] closes) => closes.Select(c => C(c, c)).ToList();

    private static List<Candle> Trend(int n, decimal start, decimal step) =>
        Enumerable.Range(0, n).Select(i => C(start + i * step, start + i * step)).ToList();

    [Fact]
    public void Steady_trend_has_no_ema_flips() =>
        Assert.Equal(0, WhipsawShadowGate.CountEmaFlips(Trend(80, 100m, 1m), 10));

    [Fact]
    public void Too_few_candles_gives_zero_flips() =>
        Assert.Equal(0, WhipsawShadowGate.CountEmaFlips(Trend(30, 100m, 1m), 10));

    [Fact]
    public void Choppy_tail_after_uptrend_produces_flips()
    {
        var list = Trend(60, 100m, 1m);                      // EMA20 well above EMA50
        for (var i = 1; i <= 10; i++)                         // steep reversal drags EMA20 below EMA50
            list.Add(C(0m, 160m - i * 40m));
        Assert.True(WhipsawShadowGate.CountEmaFlips(list, 10) >= 1);
    }

    [Fact]
    public void Alternating_5m_candles_are_counted()
    {
        var bars = new List<Candle> { C(10, 11), C(11, 10), C(10, 11), C(11, 10), C(10, 11) };
        Assert.Equal(4, WhipsawShadowGate.CountAlternations(bars, 5));
    }

    [Fact]
    public void Same_direction_5m_candles_do_not_alternate()
    {
        var bars = new List<Candle> { C(10, 11), C(11, 12), C(12, 13), C(13, 14), C(14, 15) };
        Assert.Equal(0, WhipsawShadowGate.CountAlternations(bars, 5));
    }

    [Fact]
    public void Doji_candles_are_skipped()
    {
        var bars = new List<Candle> { C(10, 11), C(11, 11), C(11, 12) };
        Assert.Equal(0, WhipsawShadowGate.CountAlternations(bars, 5));
    }

    [Fact]
    public void Calm_market_is_Normal()
    {
        var r = WhipsawShadowGate.Evaluate(Trend(80, 100m, 1m), Trend(5, 100m, 1m), 102m, 100m, 102m, 100m);
        Assert.Equal(VolatilityState.Normal, r.State);
        Assert.Equal(0, r.InstabilityScore);
    }

    [Fact]
    public void Timeframe_disagreement_alone_is_Normal_with_one_reason()
    {
        var r = WhipsawShadowGate.Evaluate(Trend(80, 100m, 1m), Trend(5, 100m, 1m), 102m, 100m, 98m, 100m);
        Assert.Equal(1, r.InstabilityScore);
        Assert.Equal(VolatilityState.Normal, r.State);
        Assert.Contains("disagree", r.Reasons[0]);
    }

    [Fact]
    public void Two_signals_is_Caution_and_three_is_Severe()
    {
        var alt = new List<Candle> { C(10, 11), C(11, 10), C(10, 11), C(11, 10), C(10, 11) };

        var two = WhipsawShadowGate.Evaluate(Trend(80, 100m, 1m), alt, 102m, 100m, 98m, 100m);
        Assert.Equal(2, two.InstabilityScore);
        Assert.Equal(VolatilityState.Caution, two.State);

    }

    [Theory]
    [InlineData(0, VolatilityState.Normal)]
    [InlineData(1, VolatilityState.Normal)]
    [InlineData(2, VolatilityState.Caution)]
    [InlineData(3, VolatilityState.SevereWhipsaw)]
    public void State_thresholds(int score, VolatilityState expected) =>
        Assert.Equal(expected, WhipsawShadowGate.StateFor(score));

    [Fact]
    public void Missing_ema_gives_no_disagreement()
    {
        var r = WhipsawShadowGate.Evaluate(Trend(80, 100m, 1m), Trend(5, 100m, 1m), 0m, 0m, 98m, 100m);
        Assert.Equal(0, r.InstabilityScore);
    }
}
