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
/// Priority ordering of ExitDecisionService's rule ladder. The ORDER of these checks is the
/// trading logic — a position exiting on the wrong rule books the wrong price and the wrong
/// OrderPurpose — so it is pinned here rather than left to survive on reading alone. Written when
/// the ladder was lifted out of ExitWorker, to prove the move changed nothing.
///
/// Only rules reachable without the broker/DB are exercised: the T1 partial-booking branch calls
/// into PositionCloseService and needs repositories, so every case below keeps open lots at 1,
/// which is the full-exit branch of the same rule.
/// </summary>
public class ExitDecisionLadderTests
{
    private const decimal NoUnderlyingTrigger = 0m;

    /// <summary>AppErrorLogger swallows every persistence failure, so a throwing scope factory is
    /// safe here — the SL_M fallback rule logs through it and must still return its exit reason.</summary>
    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException("no DB in these tests");
    }

    private static ExitDecisionService Build(TradingSettings? settings = null)
    {
        var systemSettings = new SystemSettingsService(
            new StaticOptionsMonitor<TradingSettings>(settings ?? new TradingSettings()));

        var errorLogger = new AppErrorLogger(
            new ThrowingScopeFactory(), NullLogger<AppErrorLogger>.Instance);

        var stopLossSync = new StopLossSyncService(
            new StopLossOrderCoordinator(NullLogger<StopLossOrderCoordinator>.Instance, systemSettings));

        var positionClose = new PositionCloseService(
            NullLogger<PositionCloseService>.Instance,
            new FakeInstrumentCache(),
            stopLossSync);

        return new ExitDecisionService(
            NullLogger<ExitDecisionService>.Instance,
            systemSettings,
            new FakeStrategySettingsService(),
            errorLogger,
            positionClose);
    }

    /// <summary>
    /// Repositories/broker are null because no case here reaches the partial-exit branch — the
    /// only path that touches them. A null-reference failure would mean a case drifted into it.
    /// </summary>
    private static Task<(string? exitReason, decimal exitPrice)> Decide(
        ExitDecisionService svc,
        Signal signal,
        decimal triggerLtp,
        decimal optionLtp,
        decimal underlyingLtp = NoUnderlyingTrigger,
        bool forceExit = false) =>
        svc.DetermineAsync(
            signal,
            triggerLtp,
            optionLtp,
            leg2CurrentLtp: 0m,
            underlyingLtp,
            forceExit,
            nowIst: IstClock.Now,
            chainBySymbol: new Dictionary<string, List<OptionChainData>>(),
            orders: null!,
            journalRepo: null!,
            orderRepo: null!,
            uow: null!,
            tradeCompletion: null!);

    private static Signal SingleLeg() => new()
    {
        Id = 1,
        Symbol = "NIFTY",
        SignalType = TradingConst.SignalType.CALL,
        StrategyType = TradingConst.StrategyType.Single,
        Quantity = 1,
        StopLossStatus = "PLACED",
    };

    [Fact]
    public async Task HeroZeroSafety_OutranksEveryOtherRule()
    {
        var signal = SingleLeg();
        signal.OptionT2 = 10m;   // target also satisfied
        signal.OptionSL = 50m;   // stop also breached

        // Premium collapsed below ₹2 — rule 1 must win over force exit and both targets.
        var (reason, price) = await Decide(Build(), signal, triggerLtp: 1.5m, optionLtp: 1.5m, forceExit: true);

        Assert.Equal(TradingConst.ExitReason.HeroZero, reason);
        Assert.Equal(1.5m, price);
    }

    [Fact]
    public async Task ForceExit_OutranksTargetsAndStops()
    {
        var signal = SingleLeg();
        signal.OptionT2 = 100m;

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 150m, optionLtp: 150m, forceExit: true);

        Assert.Equal(TradingConst.ExitReason.ForceExitEod, reason);
    }

    [Fact]
    public async Task IndexTarget1_LocksProtectionWithoutExiting()
    {
        var signal = SingleLeg();
        signal.Target1 = 20_000m;
        signal.OptionLtp = 100m;   // entry premium

        var (reason, _) = await Decide(
            Build(), signal, triggerLtp: 140m, optionLtp: 140m, underlyingLtp: 20_050m);

        Assert.Null(reason);                 // rule 3 never exits
        Assert.True(signal.IndexT1Hit);
        // lockProfit = 40 > 5  ->  entry + 40% of the gain
        Assert.Equal(116m, signal.ProtectedSL);
    }

    [Fact]
    public async Task IndexTarget1_SmallPremiumMove_ProtectsBelowEntry()
    {
        // Index reached its target but the premium barely moved: lockProfit = 3, which is NOT
        // greater than the absolute 5-rupee threshold, so protection lands at entry x 0.97 -
        // three percent BELOW cost. Pinned because it is easy to read the rule as breakeven
        // protection when it is not.
        var signal = SingleLeg();
        signal.Target1 = 20_000m;
        signal.OptionLtp = 100m;

        var (reason, _) = await Decide(
            Build(), signal, triggerLtp: 103m, optionLtp: 103m, underlyingLtp: 20_050m);

        Assert.Null(reason);
        Assert.True(signal.IndexT1Hit);
        Assert.Equal(97m, signal.ProtectedSL);
    }

    [Fact]
    public async Task ProtectedSl_IsFrozenOnceArmed_AndDoesNotTrail()
    {
        // IndexT1Hit is a one-way latch: the level is computed once, at the instant the index
        // touched T1, and never recalculated. A later premium high does NOT raise it.
        var signal = SingleLeg();
        signal.IndexT1Hit = true;
        signal.OptionLtp = 100m;
        signal.ProtectedSL = 116m;

        await Decide(Build(), signal, triggerLtp: 200m, optionLtp: 200m);

        Assert.Equal(116m, signal.ProtectedSL);
    }

    [Fact]
    public async Task ProtectedSl_DoesNotFireWhileUnarmed()
    {
        // ProtectedSL == 0 is the real invariant: nothing has armed protection, so rule 4 must
        // stay silent however far the premium falls.
        //
        // This previously asserted that ProtectedSL > 0 with IndexT1Hit == false must NOT fire.
        // That combination used to be unreachable — condition 3 was the only writer of
        // ProtectedSL and it always set IndexT1Hit alongside. Condition 3b (partial-T1 breakeven
        // arm) now writes ProtectedSL on a PREMIUM touch without any index target, so rule 4 is
        // gated on ProtectedSL alone and that combination is both reachable and required to fire.
        // BreakevenArmTests covers the armed case.
        var signal = SingleLeg();
        signal.IndexT1Hit = false;
        signal.ProtectedSL = 0m;

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 90m, optionLtp: 90m);

        Assert.Null(reason);
    }

    [Fact]
    public async Task ProtectedSl_FiresWhenArmedWithoutIndexTarget1()
    {
        // The new path: armed by the partial-T1 breakeven rule, no index target involved.
        var signal = SingleLeg();
        signal.IndexT1Hit = false;
        signal.ProtectedSL = 200m;

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 90m, optionLtp: 90m);

        Assert.Equal("PROTECTED_SL_HIT", reason);
    }

    [Fact]
    public async Task ProtectedSl_FiresOnceIndexTarget1IsHit()
    {
        var signal = SingleLeg();
        signal.IndexT1Hit = true;
        signal.ProtectedSL = 116m;

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 110m, optionLtp: 110m);

        Assert.Equal("PROTECTED_SL_HIT", reason);
    }

    [Fact]
    public async Task SingleLeg_WithRestingBrokerStop_DoesNotFireMarketExit()
    {
        // The broker SL_M owns this trigger — ExitWorker must NOT also fire a MARKET sell, or the
        // position is sold twice.
        var signal = SingleLeg();
        signal.OptionSL = 100m;
        signal.StopLossStatus = "PLACED";

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 95m, optionLtp: 95m);

        Assert.Null(reason);
    }

    [Fact]
    public async Task SingleLeg_WithoutRestingStop_FiresBoundedMarketFallback()
    {
        var signal = SingleLeg();
        signal.OptionSL = 100m;
        signal.StopLossStatus = "FAILED";
        signal.LastSlUpdateTime = IstClock.Now.AddSeconds(-30);   // unprotected well past the 5s cap

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 95m, optionLtp: 95m);

        Assert.Equal(TradingConst.ExitReason.OptionSlHit, reason);
    }

    [Fact]
    public async Task SingleLeg_WithinFallbackTimeout_HoldsInsteadOfExiting()
    {
        var signal = SingleLeg();
        signal.OptionSL = 100m;
        signal.StopLossStatus = "FAILED";
        signal.LastSlUpdateTime = IstClock.Now;   // just lost protection — retries get their window

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 95m, optionLtp: 95m);

        Assert.Null(reason);
    }

    [Fact]
    public async Task Spread_KeepsThePollingStopLossCheck()
    {
        // Spreads have no single broker order to protect them, so the crossing check still exits.
        var signal = SingleLeg();
        signal.StrategyType = TradingConst.StrategyType.BullSpread;
        signal.OptionSL = 100m;

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 95m, optionLtp: 95m);

        Assert.Equal(TradingConst.ExitReason.OptionSlHit, reason);
    }

    [Fact]
    public async Task Target2_OutranksTarget1()
    {
        var signal = SingleLeg();
        signal.OptionT1 = 120m;
        signal.OptionT2 = 150m;   // both satisfied at 160

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 160m, optionLtp: 160m);

        Assert.Equal(TradingConst.ExitReason.OptionT2Hit, reason);
    }

    [Fact]
    public async Task Target1_WithSingleLot_ExitsFullyInsteadOfBooking()
    {
        var signal = SingleLeg();
        signal.OptionT1 = 120m;
        signal.Quantity = 1;      // only one lot -> no partial booking

        var (reason, _) = await Decide(Build(), signal, triggerLtp: 125m, optionLtp: 125m);

        Assert.Equal(TradingConst.ExitReason.OptionT1Hit, reason);
    }

    [Fact]
    public async Task IndexStopLoss_FiresForCallBelowStop()
    {
        var signal = SingleLeg();
        signal.StopLoss = 19_900m;

        var (reason, _) = await Decide(
            Build(), signal, triggerLtp: 95m, optionLtp: 95m, underlyingLtp: 19_850m);

        Assert.Equal(TradingConst.ExitReason.IndexSlHit, reason);
    }

    [Fact]
    public async Task NothingTriggered_HoldsThePosition()
    {
        var signal = SingleLeg();
        signal.OptionSL = 50m;
        signal.OptionT1 = 200m;
        signal.OptionT2 = 300m;

        var (reason, price) = await Decide(Build(), signal, triggerLtp: 100m, optionLtp: 100m);

        Assert.Null(reason);
        Assert.Equal(0m, price);
    }
}
