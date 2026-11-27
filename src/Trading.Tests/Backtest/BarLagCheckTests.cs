using Trading.Backtest.Data;
using Trading.Backtest.Simulation;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>BarLagCheck: could a not-yet-final newest 5m bar turn a StopNoiseGate block into a pass?</summary>
public class BarLagCheckTests
{
    private static Bar[] Bars(params double[] ranges)
    {
        var t = new DateTime(2026, 11, 26, 4, 0, 0, DateTimeKind.Utc);
        return ranges.Select((r, i) => new Bar(t.AddMinutes(5 * i), 100, 100 + r, 100, 100)).ToArray();
    }

    // Signal 10453 (2026-11-26): five bars 199.2 pts total + newest 37.0 -> avg 39.37, stop 38.89.
    private static readonly Bar[] Nifty1045 = Bars(70.2, 29.3, 45.65, 20.4, 33.65, 37.0);

    [Fact]
    public void BlockWithinTheNewestBarsInfluence_IsFlipPossible()
    {
        var r = BarLagCheck.Evaluate("NIFTY", DateTime.Today, 10453, "LIVE_ONLY", Nifty1045, 6, 38.89, 6, 1.0);

        Assert.Equal(BarLagCheck.FlipPossible, r.Verdict);
        Assert.Equal(39.37, r.AvgRange, 2);
        Assert.True(r.Shortfall > 0 && r.Shortfall <= r.MaxRequiredDrop);   // 0.48 <= 37.0/6 = 6.17
    }

    [Fact]
    public void BlockFarBeyondTheNewestBarsInfluence_IsFlipImpossible()
    {
        var r = BarLagCheck.Evaluate("NIFTY", DateTime.Today, null, "BACKTEST_ONLY", Nifty1045, 6, 20.0, 6, 1.0);

        Assert.Equal(BarLagCheck.FlipImpossible, r.Verdict);
    }

    [Fact]
    public void StopAtOrAboveRequired_IsNotBlocked() =>
        Assert.Equal(BarLagCheck.NotBlocked, BarLagCheck.Evaluate("NIFTY", DateTime.Today, null, "", Nifty1045, 6, 45.0, 6, 1.0).Verdict);

    [Fact]
    public void TooFewBars_IsNoData() =>
        Assert.Equal(BarLagCheck.NoData, BarLagCheck.Evaluate("NIFTY", DateTime.Today, null, "", Bars(30, 30), 2, 20.0, 6, 1.0).Verdict);

    [Fact]
    public void RatioScalesTheMaxDrop()
    {
        var r = BarLagCheck.Evaluate("NIFTY", DateTime.Today, null, "", Nifty1045, 6, 20.0, 6, 2.0);

        Assert.Equal(2.0 * 37.0 / 6, r.MaxRequiredDrop, 2);
    }
}
