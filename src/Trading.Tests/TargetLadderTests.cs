using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.Exits;
using Xunit;
using Action = Trading.Worker.Exits.ExitDecisionService.LadderAction;

namespace Trading.Tests;

/// <summary>
/// Target ladder (EnableTargetLadder): with 4 lots sell 2 at T1 (1R), 1 at TargetLadderMidR (3R)
/// and the last at TargetLadderFinalR (5R). Off by default - with it off, conditions 7 and 8 behave
/// exactly as before.
///
/// Paths that place a partial SELL need a broker and repositories, so the routing is tested through
/// the pure DecideLadder / ShouldPlacePartial, and the full DetermineAsync is driven only on paths
/// that return without touching the broker (repositories are null and would throw if touched).
/// </summary>
public class TargetLadderTests
{
    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException("no DB in these tests");
    }

    // Entry 100, stop 80 (R = 20), T1 120 (1:1 at the default 1.5/1.5 multipliers), T2 126 (1.3R).
    // Ladder: mid = 100 + 3*20 = 160, final = 100 + 5*20 = 200.
    private static Signal Position(int lots = 4, int remaining = 0) => new()
    {
        Id = 1,
        Symbol = "NIFTY",
        SignalType = TradingConst.SignalType.CALL,
        StrategyType = TradingConst.StrategyType.Single,
        Status = SignalStatus.Active,
        Quantity = lots,
        RemainingQty = remaining,
        StopLossStatus = "PLACED",
        OptionLtp = 100m,
        OptionSL = 80m,
        OptionT1 = 120m,
        OptionT2 = 126m,
    };

    private static FakeStrategySettingsService LadderOn() =>
        new FakeStrategySettingsService()
            .Set("EnableTargetLadder", 1)
            .Set("EnableBreakevenArm", 0);      // keep the arm out of these paths

    private static ExitDecisionService Build(FakeStrategySettingsService settings)
    {
        var systemSettings = new SystemSettingsService(
            new StaticOptionsMonitor<TradingSettings>(new TradingSettings()));
        var errorLogger = new AppErrorLogger(new ThrowingScopeFactory(), NullLogger<AppErrorLogger>.Instance);
        var stopLossSync = new StopLossSyncService(
            new StopLossOrderCoordinator(NullLogger<StopLossOrderCoordinator>.Instance, systemSettings));
        var positionClose = new PositionCloseService(
            NullLogger<PositionCloseService>.Instance, new FakeInstrumentCache(), stopLossSync);

        return new ExitDecisionService(
            NullLogger<ExitDecisionService>.Instance, systemSettings, settings, errorLogger, positionClose);
    }

    private static Task<(string? exitReason, decimal exitPrice)> Decide(
        ExitDecisionService svc, Signal signal, decimal optionLtp) =>
        svc.DetermineAsync(
            signal, triggerLtp: optionLtp, optionLtp: optionLtp,
            leg2CurrentLtp: 0m, underlyingLtp: 0m, forceExit: false,
            nowIst: IstClock.Now,
            chainBySymbol: new Dictionary<string, List<OptionChainData>>(),
            orders: null!, journalRepo: null!, orderRepo: null!, uow: null!, tradeCompletion: null!);

    // ── levels ───────────────────────────────────────────────────────────────

    [Fact]
    public void Levels_AreThreeAndFiveR_FromTheStoredT1()
    {
        var lv = ExitDecisionService.LadderLevels(Position(), 1.5m, 1.5m, 3m, 5m);

        Assert.Equal((160m, 200m), lv);
    }

    [Fact]
    public void Levels_RecoverROriginalStop_WithNonDefaultMultipliers()
    {
        // T1 at 2.0 ATR against a 1.0 ATR stop => T1 is 2R. T1 120 => R = 10 => 3R 130, 5R 150.
        var lv = ExitDecisionService.LadderLevels(Position(), 2.0m, 1.0m, 3m, 5m);

        Assert.Equal((130m, 150m), lv);
    }

    [Fact]
    public void Levels_IgnoreOptionSl_WhichMovesToEntryAfterThePartial()
    {
        var s = Position();
        s.OptionSL = s.OptionLtp;       // breakeven after the first partial

        Assert.Equal((160m, 200m), ExitDecisionService.LadderLevels(s, 1.5m, 1.5m, 3m, 5m));
    }

    [Theory]
    [InlineData(0, 5)]      // mid not positive
    [InlineData(5, 3)]      // final not above mid
    public void Levels_NullWhenMisconfigured(double mid, double final)
        => Assert.Null(ExitDecisionService.LadderLevels(Position(), 1.5m, 1.5m, (decimal)mid, (decimal)final));

    [Fact]
    public void Levels_NullWhenT1IsNotAboveEntry()
    {
        var s = Position();
        s.OptionT1 = 100m;

        Assert.Null(ExitDecisionService.LadderLevels(s, 1.5m, 1.5m, 3m, 5m));
    }

    // ── routing: the 4-lot path 4 -> 2 -> 1 -> 0 ─────────────────────────────

    [Theory]
    [InlineData(110, 4, false, Action.Hold)]         // below T1
    [InlineData(120, 4, false, Action.SellAtT1)]     // T1: sell 2 of 4
    [InlineData(126, 2, true, Action.Hold)]          // old T2 (1.3R) is ignored
    [InlineData(150, 2, true, Action.Hold)]          // between T1 and 3R: runners hold
    [InlineData(160, 2, true, Action.SellAtMid)]     // 3R: sell 1 of 2
    [InlineData(180, 1, true, Action.Hold)]          // last lot runs past 3R
    [InlineData(200, 1, true, Action.FinalExit)]     // 5R: exit the rest
    [InlineData(250, 2, true, Action.FinalExit)]     // gap straight past 5R
    public void Routing_FourLotPath(double ltp, int openLots, bool partialDone, Action expected)
        => Assert.Equal(expected,
            ExitDecisionService.DecideLadder((decimal)ltp, 120m, 160m, 200m, openLots, partialDone));

    [Fact]
    public void Routing_GapPastT1ToMid_BooksTheT1SaleFirst()
        => Assert.Equal(Action.SellAtT1,
            ExitDecisionService.DecideLadder(165m, 120m, 160m, 200m, openLots: 4, partialDone: false));

    [Fact]
    public void Routing_OneLotTrade_KeepsFullExitAtT1()
        => Assert.Equal(Action.T1FullExit,
            ExitDecisionService.DecideLadder(120m, 120m, 160m, 200m, openLots: 1, partialDone: false));

    [Fact]
    public void Routing_AfterAPartial_LastLotIsNotClosedAtT1()
        => Assert.Equal(Action.Hold,
            ExitDecisionService.DecideLadder(121m, 120m, 160m, 200m, openLots: 1, partialDone: true));

    // ── one sale per stage, never two in flight ──────────────────────────────

    [Theory]
    [InlineData(1, 0, false, true)]     // first sale
    [InlineData(1, 1, false, false)]    // T1 stage already done
    [InlineData(2, 1, false, true)]     // middle-target sale after the T1 fill
    [InlineData(2, 2, false, false)]    // middle stage already done
    [InlineData(2, 1, true, false)]     // a partial SELL still pending at the broker
    [InlineData(1, 0, true, false)]
    public void ShouldPlacePartial_OnePerStage(int stage, int completed, bool inFlight, bool expected)
        => Assert.Equal(expected, PositionCloseService.ShouldPlacePartial(stage, completed, inFlight));

    // ── through DetermineAsync (paths that do not touch the broker) ──────────

    [Fact]
    public async Task LadderOn_FinalTarget_ExitsWithOptionT5Hit()
    {
        var (reason, price) = await Decide(Build(LadderOn()), Position(lots: 4, remaining: 1), 200m);

        Assert.Equal(TradingConst.ExitReason.OptionT5Hit, reason);
        Assert.Equal(200m, price);
    }

    [Fact]
    public async Task LadderOn_OldT2_NoLongerClosesTheRunners()
    {
        var (reason, _) = await Decide(Build(LadderOn()), Position(lots: 4, remaining: 2), 130m);

        Assert.Null(reason);
    }

    [Fact]
    public async Task LadderOn_OneLot_ExitsAtT1AsBefore()
    {
        var (reason, _) = await Decide(Build(LadderOn()), Position(lots: 1), 120m);

        Assert.Equal(TradingConst.ExitReason.OptionT1Hit, reason);
    }

    [Fact]
    public async Task LadderOff_T2StillFullExit_Unchanged()
    {
        var off = new FakeStrategySettingsService().Set("EnableBreakevenArm", 0);

        var (reason, _) = await Decide(Build(off), Position(lots: 4, remaining: 2), 126m);

        Assert.Equal(TradingConst.ExitReason.OptionT2Hit, reason);
    }

    [Fact]
    public async Task LadderOn_StopsStillWinOverTargets()
    {
        // Breakeven stop armed (ProtectedSL = entry): a drop to entry exits before any ladder check.
        var s = Position(lots: 4, remaining: 2);
        s.ProtectedSL = 100m;

        var (reason, _) = await Decide(Build(LadderOn()), s, 100m);

        Assert.Equal("PROTECTED_SL_HIT", reason);
    }

    [Fact]
    public void Defaults_AreThreeAndFiveR()
    {
        Assert.Equal(3m, ExitDecisionService.DefaultLadderMidR);
        Assert.Equal(5m, ExitDecisionService.DefaultLadderFinalR);
    }
}
