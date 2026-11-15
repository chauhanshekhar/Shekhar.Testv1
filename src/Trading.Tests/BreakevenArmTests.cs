using Trading.Application.Interfaces;
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
/// Condition 3b - sell half and arm breakeven at entry + BreakevenBufferPoints.
///
/// Once the premium reaches entry + BreakevenBufferPoints (per symbol, StrategySettings; e.g.
/// entry 140 + 15 = 155) sell 50% of the open lots and move the stop on the rest to entry (140).
/// OptionSL and OptionT1/T2 are untouched. Gated by EnableBreakevenArm. At 1 lot half cannot be
/// sold, so only the breakeven stop is armed. The sell decision (ShouldSellHalfAtArm) is tested
/// directly; the sale itself is PositionCloseService.HandlePartialExitAsync, which needs a broker
/// and repositories and is not driven from here.
///
/// Replaces the earlier arm-only form (arm at min(ATR level, entry+15), stop at entry+2), where
/// BreakevenBufferPoints was the stop OFFSET and PartialT1AtrMultiplier set the arm level. Now the
/// buffer IS the arm distance, the stop goes to entry, and PartialT1AtrMultiplier is no longer read.
/// </summary>
public class BreakevenArmTests
{
    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException("no DB in these tests");
    }

    private static ExitDecisionService Build(
        IStrategySettingsService? strategySettings = null,
        TradingSettings? settings = null)
    {
        var systemSettings = new SystemSettingsService(
            new StaticOptionsMonitor<TradingSettings>(settings ?? new TradingSettings()));

        var errorLogger = new AppErrorLogger(
            new ThrowingScopeFactory(), NullLogger<AppErrorLogger>.Instance);

        var stopLossSync = new StopLossSyncService(
            new StopLossOrderCoordinator(NullLogger<StopLossOrderCoordinator>.Instance, systemSettings));

        var positionClose = new PositionCloseService(
            NullLogger<PositionCloseService>.Instance, new FakeInstrumentCache(), stopLossSync);

        return new ExitDecisionService(
            NullLogger<ExitDecisionService>.Instance,
            systemSettings,
            strategySettings ?? new FakeStrategySettingsService(),
            errorLogger,
            positionClose);
    }

    private static Task<(string? exitReason, decimal exitPrice)> Decide(
        ExitDecisionService svc, Signal signal, decimal optionLtp) =>
        svc.DetermineAsync(
            signal, triggerLtp: optionLtp, optionLtp: optionLtp,
            leg2CurrentLtp: 0m, underlyingLtp: 0m, forceExit: false,
            nowIst: IstClock.Now,
            chainBySymbol: new Dictionary<string, List<OptionChainData>>(),
            orders: null!, journalRepo: null!, orderRepo: null!, uow: null!, tradeCompletion: null!);

    /// <summary>Entry 100, T1 130, default buffer 15 => arms at 115, stop moves to entry 100.</summary>
    private static Signal Position() => new()
    {
        Id = 1,
        Symbol = "NIFTY",
        SignalType = TradingConst.SignalType.CALL,
        StrategyType = TradingConst.StrategyType.Single,
        Quantity = 1,
        StopLossStatus = "PLACED",
        OptionLtp = 100m,   // entry premium
        OptionSL = 80m,
        OptionT1 = 130m,
        OptionT2 = 150m,
    };

    // ── arming ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(101)]
    [InlineData(110)]
    [InlineData(114.9)]
    public async Task BelowEntryPlusBuffer_DoesNotArm(double ltp)
    {
        var signal = Position();

        var (reason, _) = await Decide(Build(), signal, (decimal)ltp);

        Assert.Null(reason);
        Assert.Equal(0m, signal.ProtectedSL);
    }

    [Theory]
    [InlineData(115)]     // exactly entry + 15
    [InlineData(125)]
    public async Task AtOrAboveEntryPlusBuffer_ArmsStopAtEntryButDoesNotExit(double ltp)
    {
        var signal = Position();

        var (reason, _) = await Decide(Build(), signal, (decimal)ltp);

        Assert.Null(reason);                       // arming must never itself exit
        Assert.Equal(100m, signal.ProtectedSL);    // stop moved to entry
    }

    [Fact]
    public async Task Arming_LeavesStopLossAndTargetsUntouched()
    {
        var signal = Position();

        await Decide(Build(), signal, 120m);

        Assert.Equal(80m, signal.OptionSL);
        Assert.Equal(130m, signal.OptionT1);
        Assert.Equal(150m, signal.OptionT2);
    }

    // ── the point of the feature: price comes back ───────────────────────────

    [Fact]
    public async Task OnceArmed_PriceBackToEntry_Exits()
    {
        var signal = Position();
        var svc = Build();

        await Decide(svc, signal, 120m);
        Assert.Equal(100m, signal.ProtectedSL);

        var (reason, price) = await Decide(svc, signal, 100m);

        Assert.Equal("PROTECTED_SL_HIT", reason);
        Assert.Equal(100m, price);
    }

    [Fact]
    public async Task WithoutTheArm_TheSameRoundTripIsNotCaughtAtAll()
    {
        // Broker-side SL_M owns the stop (StopLossStatus=PLACED), so with the arm off the
        // round-trip through entry passes unnoticed — the give-back the arm exists to prevent.
        var off = new FakeStrategySettingsService().Set("EnableBreakevenArm", 0);
        var signal = Position();
        var svc = Build(off);

        await Decide(svc, signal, 120m);
        Assert.Equal(0m, signal.ProtectedSL);

        var (atEntry, _) = await Decide(svc, signal, 100m);
        Assert.Null(atEntry);

        var (atStop, _) = await Decide(svc, signal, 80m);
        Assert.Null(atStop);
    }

    [Fact]
    public async Task ArmedStopDoesNotFireWhilePriceStaysAbove()
    {
        var signal = Position();
        var svc = Build();

        await Decide(svc, signal, 120m);
        var (reason, _) = await Decide(svc, signal, 101m);

        Assert.Null(reason);
    }

    // ── settings: nothing hard-coded, per symbol ─────────────────────────────

    [Fact]
    public async Task ArmDistance_IsTheConfiguredBuffer()
    {
        var s = new FakeStrategySettingsService().Set("BreakevenBufferPoints", 25m);
        var signal = Position();
        var svc = Build(s);

        await Decide(svc, signal, 124m);
        Assert.Equal(0m, signal.ProtectedSL);        // 124 < 100 + 25

        await Decide(svc, signal, 125m);
        Assert.Equal(100m, signal.ProtectedSL);
    }

    [Fact]
    public async Task ArmDistance_UsesTheSignalsSymbol()
    {
        var recording = new SymbolRecordingSettings();
        var signal = Position();
        signal.Symbol = "SENSEX";

        await Decide(Build(recording), signal, 120m);

        Assert.Contains(("BreakevenBufferPoints", "SENSEX"), recording.Reads);
    }

    [Fact]
    public async Task ArmIsIndependentOfAtrTarget1Multiplier()
    {
        var signal = Position();
        var svc = Build(settings: new TradingSettings { AtrTarget1Multiplier = 3.0m });

        await Decide(svc, signal, 106m);
        Assert.Equal(0m, signal.ProtectedSL);
    }

    [Fact]
    public void DefaultBuffer_IsFifteen()
        => Assert.Equal(15m, ExitDecisionService.DefaultBreakevenBufferPoints);

    // ── sell half ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2, SignalStatus.Active, true)]
    [InlineData(4, SignalStatus.Active, true)]
    [InlineData(1, SignalStatus.Active, false)]                 // half a lot cannot be sold
    [InlineData(0, SignalStatus.Active, false)]
    [InlineData(2, SignalStatus.PartialExitPending, false)]     // a sell is already in flight
    [InlineData(2, SignalStatus.ExitPending, false)]
    public void ShouldSellHalfAtArm_Decision(int lots, string status, bool expected)
        => Assert.Equal(expected, ExitDecisionService.ShouldSellHalfAtArm(lots, status));

    [Fact]
    public async Task OneLot_ArmsStopWithoutAnySale()
    {
        // journalRepo/orders are null: a partial sale attempt would throw.
        var signal = Position();
        signal.Status = SignalStatus.Active;
        signal.Quantity = 1;

        var (reason, _) = await Decide(Build(), signal, 120m);

        Assert.Null(reason);
        Assert.Equal(100m, signal.ProtectedSL);
    }

    [Fact]
    public async Task PartialSaleSwitchOff_ArmsStopAndNeverSells()
    {
        // 2 lots + Active would normally sell half; journalRepo/orders are null so any attempt throws.
        var off = new FakeStrategySettingsService().Set("EnableBreakevenPartialSale", 0);
        var signal = Position();
        signal.Status = SignalStatus.Active;
        signal.Quantity = 2;
        signal.RemainingQty = 2;

        var (reason, _) = await Decide(Build(off), signal, 120m);

        Assert.Null(reason);
        Assert.Equal(100m, signal.ProtectedSL);   // breakeven stop still armed
    }

    [Fact]
    public async Task PartialSaleSwitch_IsReadPerSymbol()
    {
        var recording = new SymbolRecordingSettings();
        var signal = Position();
        signal.Symbol = "BANKNIFTY";
        signal.Status = SignalStatus.Active;

        await Decide(Build(recording), signal, 120m);

        Assert.Contains(("EnableBreakevenPartialSale", "BANKNIFTY"), recording.GetBoolReads);
    }

    // ── regressions ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IndexT1Path_StillArmsAndExits()
    {
        var signal = Position();
        signal.Target1 = 20_000m;
        var svc = Build();

        await svc.DetermineAsync(
            signal, triggerLtp: 120m, optionLtp: 120m, leg2CurrentLtp: 0m,
            underlyingLtp: 20_100m,
            forceExit: false, nowIst: IstClock.Now,
            chainBySymbol: new Dictionary<string, List<OptionChainData>>(),
            orders: null!, journalRepo: null!, orderRepo: null!, uow: null!, tradeCompletion: null!);

        Assert.True(signal.IndexT1Hit);
        Assert.True(signal.ProtectedSL > 0);
    }

    [Fact]
    public async Task TargetsStillOutrankTheArmedStop()
    {
        var signal = Position();
        var svc = Build();

        await Decide(svc, signal, 120m);
        var (reason, _) = await Decide(svc, signal, 150m);

        Assert.Equal(TradingConst.ExitReason.OptionT2Hit, reason);
    }

    [Fact]
    public async Task ArmIsOneWay_NeverLoweredOnceSet()
    {
        var signal = Position();
        var svc = Build();

        await Decide(svc, signal, 120m);
        Assert.Equal(100m, signal.ProtectedSL);

        await Decide(svc, signal, 200m);
        Assert.Equal(100m, signal.ProtectedSL);
    }

    private sealed class SymbolRecordingSettings : IStrategySettingsService
    {
        public List<(string Key, string? Symbol)> Reads { get; } = new();
        private readonly FakeStrategySettingsService _inner = new();
        public decimal GetDecimal(string k, string? s, decimal d) { Reads.Add((k, s)); return _inner.GetDecimal(k, s, d); }
        public int GetInt(string k, string? s, int d) => _inner.GetInt(k, s, d);
        public List<(string Key, string? Symbol)> GetBoolReads { get; } = new();
        public bool GetBool(string k, string? s, bool d) { GetBoolReads.Add((k, s)); return _inner.GetBool(k, s, d); }
        public string GetString(string k, string? s, string d) => _inner.GetString(k, s, d);
        public bool IsSettingActive(string k, string? s = null) => _inner.IsSettingActive(k, s);
        public Task ReloadAsync(CancellationToken ct = default) => _inner.ReloadAsync(ct);
    }
}
