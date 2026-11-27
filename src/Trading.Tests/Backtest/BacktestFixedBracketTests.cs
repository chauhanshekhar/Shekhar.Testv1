using Trading.Backtest.Simulation;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// --fixed-bracket (research only): stop and target a fixed number of premium points from entry, full exit,
/// none of the live ladder. P&amp;L is per lot counted as 1 unit.
/// </summary>
public class BacktestFixedBracketTests
{
    [Fact]
    public void TargetTouched_ExitsFullAtPlusPoints()
    {
        var r = ExitReplay.RunFixedBracket([5, 12, 31, -50], 30, lots: 2);

        Assert.Equal("FIXED_TARGET_HIT", r.ExitReason);
        Assert.Equal(60, r.PnlPoints);          // 2 lots x +30, filled at the level not at 31
        Assert.Equal(2, r.ExitStep);
        Assert.Equal(2, r.Orders);
    }

    [Fact]
    public void StopTouched_ExitsFullAtMinusPoints()
    {
        var r = ExitReplay.RunFixedBracket([-10, -29, -33, 40], 30, lots: 2);

        Assert.Equal("FIXED_STOP_HIT", r.ExitReason);
        Assert.Equal(-60, r.PnlPoints);
        Assert.Equal(2, r.ExitStep);
    }

    [Fact]
    public void SeparateTarget_StopStaysAtThirty_TargetMovesToSixty()
    {
        // +31 is past a 30 target but short of a 60 one: the trade must still be open.
        var held = ExitReplay.RunFixedBracket([10, 31, 45, 61], 30, lots: 1, targetPoints: 60);
        Assert.Equal("FIXED_TARGET_HIT", held.ExitReason);
        Assert.Equal(60, held.PnlPoints);
        Assert.Equal(3, held.ExitStep);

        var stopped = ExitReplay.RunFixedBracket([10, 31, -30], 30, lots: 1, targetPoints: 60);
        Assert.Equal("FIXED_STOP_HIT", stopped.ExitReason);
        Assert.Equal(-30, stopped.PnlPoints);
    }

    [Fact]
    public void NeitherTouched_ForceExitsAtTheLastPrice()
    {
        var r = ExitReplay.RunFixedBracket([4, 18, 9], 30, lots: 1);

        Assert.Equal(9, r.PnlPoints);
        Assert.Equal(2, r.ExitStep);
        Assert.Equal(18, r.MaxFavorable);
    }

    [Fact]
    public void NoBreakevenOrTrailing_APullbackAfterAGainIsStillHeldToTheStop()
    {
        // The live ladder would lock profit here; the fixed bracket must not.
        var r = ExitReplay.RunFixedBracket([20, 25, -5, -31], 30, lots: 1);

        Assert.Equal("FIXED_STOP_HIT", r.ExitReason);
        Assert.Equal(-30, r.PnlPoints);
    }
}
