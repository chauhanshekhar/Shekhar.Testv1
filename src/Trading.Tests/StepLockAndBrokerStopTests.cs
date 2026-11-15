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

namespace Trading.Tests;

/// <summary>
/// 2026-09-30 live session fixes:
///  1. the broker SL_M follows the highest armed stop (breakeven / step-lock / index-T1 lock),
///     so Groww executes those exits instead of a cancel-then-MARKET race;
///  2. step-lock: entry+buffer -> entry, T1 -> entry+buffer, T2 -> T1, ladder 3R -> T2;
///  3. never a second resting stop / never a stop before the entry fills.
/// </summary>
public class StepLockAndBrokerStopTests
{
    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException("no DB in these tests");
    }

    private static ExitDecisionService Build(FakeStrategySettingsService settings)
    {
        var systemSettings = new SystemSettingsService(new StaticOptionsMonitor<TradingSettings>(new TradingSettings()));
        var errorLogger = new AppErrorLogger(new ThrowingScopeFactory(), NullLogger<AppErrorLogger>.Instance);
        var stopLossSync = new StopLossSyncService(
            new StopLossOrderCoordinator(NullLogger<StopLossOrderCoordinator>.Instance, systemSettings));
        var positionClose = new PositionCloseService(
            NullLogger<PositionCloseService>.Instance, new FakeInstrumentCache(), stopLossSync);
        return new ExitDecisionService(
            NullLogger<ExitDecisionService>.Instance, systemSettings, settings, errorLogger, positionClose);
    }

    private static Task<(string? exitReason, decimal exitPrice)> Decide(
        ExitDecisionService svc, Signal signal, decimal optionLtp, decimal underlying = 0m) =>
        svc.DetermineAsync(
            signal, triggerLtp: optionLtp, optionLtp: optionLtp,
            leg2CurrentLtp: 0m, underlyingLtp: underlying, forceExit: false,
            nowIst: IstClock.Now,
            chainBySymbol: new Dictionary<string, List<OptionChainData>>(),
            orders: null!, journalRepo: null!, orderRepo: null!, uow: null!, tradeCompletion: null!);

    // Entry 300, buffer 12, T1 330, T2 339 (the example agreed with the user).
    private static Signal Position() => new()
    {
        Id = 1,
        Symbol = "SENSEX",
        SignalType = TradingConst.SignalType.CALL,
        StrategyType = TradingConst.StrategyType.Single,
        Status = SignalStatus.Active,
        Quantity = 1,
        OptionLtp = 300m,
        OptionSL = 270m,
        OptionT1 = 330m,
        OptionT2 = 339m,
    };

    private static FakeStrategySettingsService StepLockOn() =>
        new FakeStrategySettingsService()
            .Set("EnableStepLock", 1)
            .Set("EnableBreakevenArm", 0)          // isolate step-lock from the arm
            .Set("BreakevenBufferPoints", 12m);

    // ── step-lock levels ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(305, 0)]       // below entry + buffer: nothing
    [InlineData(312, 300)]     // entry + buffer -> entry
    [InlineData(329.9, 300)]
    [InlineData(330, 312)]     // T1 -> entry + buffer
    [InlineData(338.9, 312)]
    [InlineData(339, 330)]     // T2 -> T1
    [InlineData(500, 330)]     // no ladder: T2 is the top step
    public void StepLockLevel_Table(double peak, double expected)
        => Assert.Equal((decimal)expected,
            ExitDecisionService.StepLockLevel(300m, 12m, 330m, 339m, null, (decimal)peak));

    [Theory]
    [InlineData(389.9, 330)]   // below the ladder 3R
    [InlineData(390, 339)]     // 3R -> T2
    public void StepLockLevel_LadderMid_LocksT2(double peak, double expected)
        => Assert.Equal((decimal)expected,
            ExitDecisionService.StepLockLevel(300m, 12m, 330m, 339m, 390m, (decimal)peak));

    [Fact]
    public async Task StepLock_UsesThePeak_SoAPullbackStillLocksT1()
    {
        // 4 lots on the target ladder, T1 half already sold (2 left): between T1 and 3R the runners
        // hold, so only the step-lock decides. Peak 345 was above T2 (339) -> stop locks at T1 (330).
        var s = Position();
        s.Quantity = 4;
        s.RemainingQty = 2;
        s.MfePrice = 345m;

        var (reason, _) = await Decide(Build(StepLockOn().Set("EnableTargetLadder", 1)), s, 335m);

        Assert.Null(reason);
        Assert.Equal(330m, s.ProtectedSL);
    }

    [Fact]
    public async Task StepLock_ExitsWhenPriceFallsToTheLock()
    {
        var s = Position();
        s.MfePrice = 345m;

        var (reason, _) = await Decide(Build(StepLockOn()), s, 329m);   // below T1 lock

        Assert.Equal("PROTECTED_SL_HIT", reason);
    }

    [Fact]
    public async Task StepLock_OffByDefault()
    {
        var s = Position();
        s.MfePrice = 345m;

        await Decide(Build(new FakeStrategySettingsService().Set("EnableBreakevenArm", 0)), s, 335m);

        Assert.Equal(0m, s.ProtectedSL);
    }

    [Fact]
    public async Task StepLock_NeverLowersAnExistingStop()
    {
        var s = Position();
        s.ProtectedSL = 331m;             // e.g. from the index-T1 lock
        s.MfePrice = 332m;                // step-lock alone would say 312

        await Decide(Build(StepLockOn()), s, 332m);

        Assert.Equal(331m, s.ProtectedSL);
    }

    [Fact]
    public async Task IndexT1Lock_NoLongerLowersABreakevenStop()
    {
        // Breakeven already armed at entry (300). Index T1 hits with little premium profit, which
        // used to set ProtectedSL = entry x 0.97 = 291 — below breakeven.
        var s = Position();
        s.ProtectedSL = 300m;
        s.Target1 = 72_900m;

        await Decide(Build(new FakeStrategySettingsService().Set("EnableBreakevenArm", 0)), s, 302m, underlying: 72_950m);

        Assert.True(s.IndexT1Hit);
        Assert.Equal(300m, s.ProtectedSL);
    }

    // ── broker stop follows the protected level ──────────────────────────────

    [Theory]
    [InlineData(270, 0, 320, 270)]      // nothing armed: original stop
    [InlineData(270, 300, 320, 300)]    // breakeven armed: broker stop moves to entry
    [InlineData(270, 330, 345, 330)]    // step-lock at T1
    [InlineData(270, 300, 300, 270)]    // protected level already at/above LTP: keep broker stop, MARKET exit handles it
    [InlineData(270, 300, 0, 300)]      // LTP unknown: still move
    [InlineData(310, 300, 320, 310)]    // trailing stop already higher than protected
    public void BrokerStopTrigger_IsTheHighestPlaceableStop(double optionSl, double protectedSl, double ltp, double expected)
        => Assert.Equal((decimal)expected,
            StopLossSyncService.BrokerStopTrigger((decimal)optionSl, (decimal)protectedSl, (decimal)ltp));

    [Fact]
    public async Task ProtectedExit_DefersToARestingBrokerStopAtThatLevel()
    {
        // The SL_M at Groww already sits at the protected level: Groww fills it. A MARKET sell here
        // would be a second SELL against one lot (rejected as a naked short).
        var s = Position();
        s.ProtectedSL = 300m;
        s.StopLossStatus = "PLACED";
        s.StopLossOrderId = "GSM1";
        s.StopLossTriggerPrice = 300m;

        var (reason, _) = await Decide(Build(new FakeStrategySettingsService().Set("EnableBreakevenArm", 0)), s, 299m);

        Assert.Null(reason);
    }

    [Fact]
    public async Task ProtectedExit_StillFiresWhenBrokerStopIsLower()
    {
        var s = Position();
        s.ProtectedSL = 300m;
        s.StopLossStatus = "PLACED";
        s.StopLossOrderId = "GSM1";
        s.StopLossTriggerPrice = 270m;      // not yet moved up

        var (reason, _) = await Decide(Build(new FakeStrategySettingsService().Set("EnableBreakevenArm", 0)), s, 299m);

        Assert.Equal("PROTECTED_SL_HIT", reason);
    }

    // ── stop-order guards ────────────────────────────────────────────────────

    [Theory]
    [InlineData(SignalStatus.Active, true)]
    [InlineData(SignalStatus.PartialExitPending, true)]
    [InlineData(SignalStatus.ExitPending, true)]
    [InlineData(SignalStatus.CloseFailed, true)]
    [InlineData(SignalStatus.OrderPlaced, false)]
    [InlineData(SignalStatus.Pending, false)]
    [InlineData(SignalStatus.OrderRejected, false)]
    public void EntryFilled_OnlyAfterTheBuyFills(string status, bool expected)
        => Assert.Equal(expected, StopLossOrderCoordinator.IsEntryFilled(status));

    [Fact]
    public async Task TryPlace_RefusesBeforeTheEntryFills()
    {
        // Signal 93 on 2026-09-30: an SL_M went out while the BUY was unconfirmed. Repositories are
        // null here — the refusal must happen before anything touches the DB or the broker.
        var coordinator = new StopLossOrderCoordinator(
            NullLogger<StopLossOrderCoordinator>.Instance,
            new SystemSettingsService(new StaticOptionsMonitor<TradingSettings>(new TradingSettings())));
        var s = Position();
        s.Status = SignalStatus.OrderPlaced;

        Assert.False(await coordinator.TryPlaceAsync(s, 270m, 1, null!, null!, null!, null!));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, "GSM1", true)]
    [InlineData("OPEN", "GSM1", true)]
    [InlineData("TRIGGER_PENDING", "GSM1", true)]
    [InlineData(OrderStatus.Cancelled, "GSM1", false)]
    [InlineData(OrderStatus.Rejected, "GSM1", false)]
    [InlineData(OrderStatus.Complete, "GSM1", false)]
    [InlineData(OrderStatus.Pending, "", false)]          // never reached the broker
    public void RestingStopRow_IsAnyLiveStopAtTheBroker(string status, string brokerId, bool expected)
        => Assert.Equal(expected, StopLossOrderCoordinator.IsRestingStopRow(new Order
        {
            OrderPurpose = OrderPurpose.StopLoss,
            Status = status,
            BrokerOrderId = brokerId,
        }));

    [Fact]
    public void RestingStopRow_IgnoresNonStopOrders()
        => Assert.False(StopLossOrderCoordinator.IsRestingStopRow(new Order
        {
            OrderPurpose = OrderPurpose.Exit,
            Status = OrderStatus.Pending,
            BrokerOrderId = "GMK1",
        }));
}
