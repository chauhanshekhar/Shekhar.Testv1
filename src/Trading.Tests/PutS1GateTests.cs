using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>PutS1Gate: the S1 PUT entry profile (PCR, 5m RSI, ATR, delta, below 5m EMA20, bearish 1m trend). All conditions at entry time only.</summary>
public class PutS1GateTests
{
    private static readonly PutS1Gate.Limits L = PutS1Gate.Defaults;

    // A profile that passes: PCR 1.05, RSI 31.5, ATR 38, delta -0.50, index 54,700 below 5m EMA20 54,750, EMA20 < EMA50
    private static string? Run(decimal? rsi = 31.5m, decimal pcr = 1.05m, decimal atr = 38m, decimal? delta = -0.50m,
        decimal ltp = 54_700m, decimal e5 = 54_750m, decimal e20 = 54_720m, decimal e50 = 54_780m)
        => PutS1Gate.Evaluate(L, rsi, pcr, atr, delta, ltp, e5, e20, e50);

    [Fact] public void MatchingProfile_Proceeds() => Assert.Null(Run());

    [Theory]
    [InlineData(1.00, true)] [InlineData(1.20, true)] [InlineData(0.99, false)] [InlineData(1.21, false)]
    public void Pcr_BoundsAreInclusive(double pcr, bool ok) => Assert.Equal(ok, Run(pcr: (decimal)pcr) is null);

    [Theory]
    [InlineData(30.0, true)] [InlineData(33.0, true)] [InlineData(29.9, false)] [InlineData(33.1, false)]
    public void Rsi_BoundsAreInclusive(double rsi, bool ok) => Assert.Equal(ok, Run(rsi: (decimal)rsi) is null);

    [Theory]
    [InlineData(30, true)] [InlineData(50, true)] [InlineData(29.9, false)] [InlineData(50.1, false)]
    public void Atr_BoundsAreInclusive(double atr, bool ok) => Assert.Equal(ok, Run(atr: (decimal)atr) is null);

    [Theory]
    [InlineData(-0.56, true)] [InlineData(-0.45, true)] [InlineData(-0.57, false)] [InlineData(-0.44, false)]
    public void Delta_BoundsAreInclusive(double delta, bool ok) => Assert.Equal(ok, Run(delta: (decimal)delta) is null);

    [Fact] public void IndexAboveFiveMinuteEma20_Blocks() => Assert.NotNull(Run(ltp: 54_760m));
    [Fact] public void OneMinuteTrendNotBearish_Blocks() => Assert.NotNull(Run(e20: 54_800m, e50: 54_780m));

    [Fact] public void MissingRsi_Blocks() => Assert.Contains("unavailable", Run(rsi: null));
    [Fact] public void MissingDelta_Blocks() => Assert.Contains("unavailable", Run(delta: null));
    [Fact] public void MissingFiveMinuteEma_Blocks() => Assert.Contains("unavailable", Run(e5: 0m));
}
