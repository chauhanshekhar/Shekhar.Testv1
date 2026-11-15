using Trading.Backtest.Entry;
using Trading.Backtest.Reporting;
using Trading.Backtest.Simulation;
using Xunit;

namespace Trading.Tests.Backtest;

public class BacktestMetricsTests
{
    private static Trade T(double pnl, int minute, string reason = "OPTION_SL_HIT", double? cost = null, bool mid = false) =>
        new("NIFTY", TradeSide.Call, BtSupport.Day.AddHours(4).AddMinutes(minute), BtSupport.Day.AddHours(4).AddMinutes(minute + 1), "AND_CHAIN",
            PremiumSource.IndexDeltaModel, 100, 15, pnl, cost, reason, 2, false, mid, reason == "OPTION_T5_HIT");

    [Fact]
    public void EmptyResultHasNullRatiosNotZeroOrInfinity()
    {
        var m = PerformanceMetrics.Compute([]);
        Assert.Equal(0, m.TotalTrades);
        Assert.Null(m.WinRatePct);
        Assert.Null(m.ProfitFactor);
        Assert.Null(m.AverageProfitPerTrade);
        Assert.Null(m.AverageWin);
        Assert.Null(m.AverageLoss);
        Assert.Null(m.NetProfitAfterCosts);
    }

    [Fact]
    public void ProfitFactorIsNullWhenThereIsNoLoss()
    {
        var m = PerformanceMetrics.Compute([T(100, 1), T(50, 5)]);
        Assert.Null(m.ProfitFactor);
        Assert.Equal(150, m.GrossProfit);
        Assert.Equal(0, m.GrossLoss);
        Assert.Null(m.AverageLoss);
    }

    [Fact]
    public void WinsLossesAndBreakEvenUseOneDefinition()
    {
        var m = PerformanceMetrics.Compute([T(100, 1), T(-40, 5), T(0.3, 9), T(-0.4, 13)]);
        Assert.Equal(1, m.WinningTrades);
        Assert.Equal(1, m.LosingTrades);
        Assert.Equal(2, m.BreakEvenTrades);                 // within +/-0.5 rupees is flat, and stays in the denominator
        Assert.Equal(25.0, m.WinRatePct);
        Assert.Equal(2.5, m.ProfitFactor);
        Assert.Equal(100, m.AverageWin);
        Assert.Equal(-40, m.AverageLoss);
    }

    [Fact]
    public void MaxDrawdownIsPeakToTroughOfCumulativePnlInExitOrder()
    {
        var m = PerformanceMetrics.Compute([T(100, 1), T(-30, 5), T(-50, 9), T(20, 13), T(-5, 17)]);
        Assert.Equal(80, m.MaxDrawdown);                    // 100 -> 20
        Assert.Equal(0, PerformanceMetrics.Compute([T(10, 1), T(10, 5)]).MaxDrawdown);
    }

    [Fact]
    public void NetAfterCostsIsNullUnlessEveryTradeHasACost()
    {
        Assert.Null(PerformanceMetrics.Compute([T(100, 1, cost: 10), T(50, 5)]).NetProfitAfterCosts);
        Assert.Equal(120, PerformanceMetrics.Compute([T(100, 1, cost: 10), T(50, 5, cost: 20)]).NetProfitAfterCosts);
    }

    [Fact]
    public void ExitReasonsMapToTheStopAndTargetCounts()
    {
        var m = PerformanceMetrics.Compute([
            T(-10, 1, "OPTION_SL_HIT"), T(-10, 5, "INDEX_SL_HIT"), T(0, 9, "PROTECTED_SL_HIT"), T(5, 13, "TRAILING_SL_HIT"),
            T(60, 17, "OPTION_T5_HIT", mid: true), T(30, 21, "FORCE_EXIT_EOD", mid: true)]);
        Assert.Equal(2, m.StopLossCount);
        Assert.Equal(2, m.BreakevenStopCount);
        Assert.Equal(2, m.Target3RCount);
        Assert.Equal(1, m.Target5RCount);
    }

    [Fact]
    public void PeriodSplitIsChronologicalAndDisjoint()
    {
        var days = Enumerable.Range(0, 20).Select(i => DateOnly.FromDateTime(BtSupport.Day.AddDays(i))).Reverse().ToList();
        var split = PeriodSplit.Create(days);

        Assert.Equal(10, split.Train.Length);
        Assert.Equal(5, split.Validation.Length);
        Assert.Equal(5, split.Test.Length);
        Assert.True(split.Train.Max() < split.Validation.Min());
        Assert.True(split.Validation.Max() < split.Test.Min());
        Assert.Empty(split.Train.Intersect(split.Validation));
        Assert.Empty(split.Validation.Intersect(split.Test));
    }
}
