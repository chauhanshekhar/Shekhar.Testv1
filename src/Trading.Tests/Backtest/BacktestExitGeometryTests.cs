using Trading.Backtest.Configurations;
using Trading.Backtest.Entry;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The stop / target width override changes only the exit geometry and leaves every default untouched.</summary>
public class BacktestExitGeometryTests
{
    private static Trading.Backtest.Simulation.ExitModel Live() => BtSupport.Exit(BtSupport.LiveLike());

    [Fact]
    public void NullOverride_isNotApplied_andAnEmptyOverrideChangesNothing()
    {
        var live = Live();
        Assert.Equal(live, new ExitGeometry().ApplyTo(live));
    }

    [Fact]
    public void Override_replacesOnlyTheNamedMultipliers()
    {
        var live = Live();
        var changed = new ExitGeometry(StopMult: 1.0, T1Mult: 3.0).ApplyTo(live);
        Assert.Equal(1.0, changed.StopMult);
        Assert.Equal(3.0, changed.T1Mult);
        Assert.Equal(live.T2Mult, changed.T2Mult);
        Assert.Equal(live.MinStop, changed.MinStop);
        Assert.Equal(live.Symbols, changed.Symbols);
    }

    [Fact]
    public void TighterStop_givesASmallerIndexStop_untilTheMinimumBinds()
    {
        var live = Live();
        var tight = new ExitGeometry(StopMult: live.StopMult / 2).ApplyTo(live);
        Assert.True(tight.IndexStopPoints(40) < live.IndexStopPoints(40));   // below the 100-point maximum for both
        Assert.Equal(live.MinStop, tight.IndexStopPoints(1));   // a tiny ATR still gets the minimum stop
    }

    [Fact]
    public void Sweep_hasNineArmsPerSide_andExactlyOneLiveBaseline()
    {
        var arms = ConfigurationCatalog.BuildExitGeometrySweep(TradeSide.Put);
        Assert.Equal(9, arms.Count);
        Assert.Single(arms, a => a.Exit is null);
        Assert.All(arms, a => Assert.Null(a.Rules));          // entries stay the live rules
    }
}
