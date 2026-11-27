using Trading.Backtest.Simulation;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>Stop-loss, breakeven, step-lock and ladder handling. Geometry: R = 10, T1 = 10, T2 = 13, 3R = 30, 5R = 50, entry premium 100, buffer 8.</summary>
public class BacktestExitReplayTests
{
    private static ExitModel Model(int lots, SymbolExitSettings cfg) =>
        new(StopMult: 1.0, T1Mult: 1.0, T2Mult: 1.3, MinStop: 20, MaxStop: 100, TrailPct: 15, Lots: lots,
            new Dictionary<string, SymbolExitSettings> { ["NIFTY"] = cfg });

    private static SymbolExitSettings Cfg(bool ladder = false, bool arm = true, bool sale = false, bool stepLock = false) =>
        new(ladder, arm, sale, stepLock, Buffer: 8, MidR: 3, FinalR: 5, Delta: 0.5, EntryPremium: 100, LotSize: 1);

    private static ReplayResult Run(double[] path, SymbolExitSettings cfg, int lots = 4, StopModel mode = StopModel.Live) =>
        ExitReplay.Run(path, r: 10, atr: 20, cfg, Model(lots, cfg), mode);

    [Fact]
    public void InitialStopExitsAtMinusROnEveryLot()
    {
        var r = Run([-5, -12], Cfg());
        Assert.Equal("OPTION_SL_HIT", r.ExitReason);
        Assert.Equal(1, r.ExitStep);
        Assert.Equal(-40, r.PnlPoints);                      // 4 lots x -10
    }

    private static ReplayResult RunCloseFill(double[] path, SymbolExitSettings cfg) =>
        ExitReplay.Run(path, r: 10, atr: 20, cfg, Model(4, cfg), StopModel.Live, stopFillAtClose: true);

    [Fact]
    public void StopFillAtClose_BooksTheCloseThatCrossedTheStop_NotTheStopLevel()
    {
        var r = RunCloseFill([8, -1], Cfg());                 // breakeven stop at 0, minute close -1
        Assert.Equal("PROTECTED_SL_HIT", r.ExitReason);
        Assert.Equal(-4, r.PnlPoints);                        // 4 lots x -1; the default books 0
    }

    [Fact]
    public void StopFillAtClose_CloseExactlyAtTheStop_IsUnchanged()
    {
        var r = RunCloseFill([8, 0], Cfg());
        Assert.Equal("PROTECTED_SL_HIT", r.ExitReason);
        Assert.Equal(0, r.PnlPoints);
    }

    [Fact]
    public void StopFillAtClose_InitialStopGapPastTheStop_BooksTheClose()
    {
        var r = RunCloseFill([-5, -12], Cfg());               // stop -10, close -12
        Assert.Equal("OPTION_SL_HIT", r.ExitReason);
        Assert.Equal(-48, r.PnlPoints);                       // 4 lots x -12 (default: -40)
    }

    private static ReplayResult RunLow(double[] path, double[] low, double[]? high = null)
    {
        var cfg = Cfg();
        return ExitReplay.Run(path, r: 10, atr: 20, cfg, Model(4, cfg), StopModel.Live, lowPath: low, highPath: high);
    }

    [Fact]
    public void StopTouchLow_MinuteLowReachesTheStopWhileTheCloseStaysAbove_ExitsAtTheStop()
    {
        var r = RunLow([-5, -5], [-12, -5]);                  // initial stop -10; close -5, estimated low -12
        Assert.Equal("OPTION_SL_HIT", r.ExitReason);
        Assert.Equal(0, r.ExitStep);
        Assert.Equal(-40, r.PnlPoints);                       // 4 lots x -10, filled at the stop
    }

    [Fact]
    public void StopTouchLow_UsesTheStopAsItStoodAtTheMinuteOpen_NotOneArmedByThatMinutesClose()
    {
        // close +8 arms the breakeven stop (0); the same minute's low of -1 is above the stop in force at its open (-10): no exit
        var r = RunLow([8, 8], [-1, 8]);
        Assert.Equal("FORCE_EXIT_EOD", r.ExitReason);
    }

    [Fact]
    public void StopTouchLow_Off_IgnoresTheLow() =>
        Assert.Equal("FORCE_EXIT_EOD", Run([-5, -5], Cfg()).ExitReason);

    [Fact]
    public void StopTouchLow_MinuteHighRaisesThePeakForTheStepLock()
    {
        var withHigh = RunLow([4, 4, 4], [4, 4, 4], [20, 4, 4]);
        var without = Run([4, 4, 4], Cfg());
        Assert.True(withHigh.MaxFavorable > without.MaxFavorable);
    }

    [Fact]
    public void StopFillDefault_StillBooksAtTheStopLevel() =>
        Assert.Equal(0, Run([8, -1], Cfg()).PnlPoints);

    [Fact]
    public void BreakevenArmMovesTheStopToEntryInTheLiveModelOnly()
    {
        var live = Run([8, -1], Cfg());
        Assert.Equal("PROTECTED_SL_HIT", live.ExitReason);
        Assert.Equal(0, live.PnlPoints);

        var fixedStop = Run([8, -1], Cfg(), mode: StopModel.Fixed);
        Assert.Equal("FORCE_EXIT_EOD", fixedStop.ExitReason);   // the fixed model never raises the stop
        Assert.Equal(-4, fixedStop.PnlPoints);
    }

    [Fact]
    public void SaleAtArmBooksHalfAtTheBufferAndProtectsTheRest()
    {
        var r = Run([8, -1], Cfg(sale: true));
        Assert.Equal(1, r.PartialSales);
        Assert.Equal(16, r.PnlPoints);                       // 2 lots sold at +8, 2 lots stopped at entry
    }

    [Fact]
    public void StepLockRaisesTheStopOneLevelBelowThePeak()
    {
        var locked = Run([12, 7], Cfg(stepLock: true));
        Assert.Equal("PROTECTED_SL_HIT", locked.ExitReason);
        Assert.Equal(36, locked.PnlPoints);                  // 2 lots at T1 (+10), 2 lots stopped at entry + buffer (+8)

        var unlocked = Run([12, 7], Cfg(stepLock: false));
        Assert.Equal("FORCE_EXIT_EOD", unlocked.ExitReason);
        Assert.Equal(34, unlocked.PnlPoints);
    }

    [Fact]
    public void LadderFinalTargetExitsEveryLotAtFiveR()
    {
        var r = Run([60], Cfg(ladder: true));
        Assert.Equal("OPTION_T5_HIT", r.ExitReason);
        Assert.True(r.ReachedFinal);
        Assert.Equal(200, r.PnlPoints);
    }

    [Fact]
    public void GapPastTheMiddleTargetBooksT1FirstThenTheMiddleSaleThenTheFinalExit()
    {
        var r = Run([35, 35, 60], Cfg(ladder: true));
        Assert.True(r.ReachedMid);
        Assert.Equal("OPTION_T5_HIT", r.ExitReason);
        Assert.Equal(2 * 10 + 1 * 30 + 1 * 50, r.PnlPoints); // 2 lots at T1, 1 at 3R, the last at 5R
        Assert.Equal(2, r.PartialSales);
    }

    [Fact]
    public void OneLotExitsFullyAtT1WhenTheLadderIsOn()
    {
        var r = Run([11], Cfg(ladder: true), lots: 1);
        Assert.Equal("PARTIAL_T1", r.ExitReason);
        Assert.Equal(10, r.PnlPoints);
    }

    [Fact]
    public void AfterTheT1SaleTheLiveModelLocksProfitAtTheIndexT1ProtectionWhileTheFixedModelKeepsTheOriginalStop()
    {
        // premium +10.5 reaches T1 (index T1 equivalent): 2 lots sell at T1, and the protected stop becomes entry + 40% of the profit (4.2)
        var live = Run([10.5, -1], Cfg(ladder: true, arm: false));
        Assert.Equal(10 * 2 + 4.2 * 2, live.PnlPoints, 6);
        Assert.Equal("PROTECTED_SL_HIT", live.ExitReason);

        var fixedStop = Run([10.5, -1], Cfg(ladder: true, arm: false), mode: StopModel.Fixed);
        Assert.Equal(10 * 2 + -1 * 2, fixedStop.PnlPoints);  // the stop never moved: the runners are still open at the end of the path
        Assert.Equal("FORCE_EXIT_EOD", fixedStop.ExitReason);
    }

    [Fact]
    public void PathWithoutAnyExitForceExitsAtTheLastClose()
    {
        var r = Run([1, 2, 3], Cfg());
        Assert.Equal("FORCE_EXIT_EOD", r.ExitReason);
        Assert.Equal(12, r.PnlPoints);
        Assert.Equal(2, r.ExitStep);
    }

    private static SymbolExitSettings T2Cfg() =>
        new(false, false, false, false, Buffer: 8, MidR: 3, FinalR: 5, Delta: 0.5, EntryPremium: 100, LotSize: 1, T2Partial: true);

    [Fact]
    public void T2Partial_SellsHalfAtT1_OneLotAtT2_AndHoldsTheLastLotToTheForceExit()
    {
        var r = Run([10.5, 13.5, 20], T2Cfg(), mode: StopModel.Fixed);
        Assert.Equal("FORCE_EXIT_EOD", r.ExitReason);        // the last lot is not closed at T1/T2
        Assert.Equal(2, r.PartialSales);
        Assert.Equal(2 * 10 + 1 * 13 + 1 * 20, r.PnlPoints); // 2 at T1, 1 at T2, last at the final close

        var legacy = Run([10.5, 13.5, 20], Cfg(arm: false), mode: StopModel.Fixed);
        Assert.Equal("OPTION_T2_HIT", legacy.ExitReason);    // legacy: T2 closes everything left
        Assert.Equal(2 * 10 + 2 * 13, legacy.PnlPoints);
    }

    [Fact]
    public void T2Partial_GapPastT2BooksT1FirstThenT2()
    {
        var r = Run([15, 15, 15], T2Cfg(), mode: StopModel.Fixed);
        Assert.Equal(2, r.PartialSales);
        Assert.Equal(2 * 10 + 1 * 13 + 1 * 15, r.PnlPoints);
    }

    [Fact]
    public void T2Partial_TwoLotTradesKeepLegacyBehaviour()
    {
        var r = Run([10.5, 13.5], T2Cfg(), lots: 2, mode: StopModel.Fixed);
        Assert.Equal("OPTION_T2_HIT", r.ExitReason);
        Assert.Equal(1 * 10 + 1 * 13, r.PnlPoints);
    }
}
