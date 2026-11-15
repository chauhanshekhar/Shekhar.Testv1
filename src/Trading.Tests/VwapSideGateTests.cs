using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// VwapSideGate: CALL needs LTP above VWAP, PUT needs LTP below VWAP. Fail-open when VWAP/LTP are unavailable.
/// </summary>
public class VwapSideGateTests
{
    private const int Both = VwapSideGate.SideBoth, CallOnly = VwapSideGate.SideCallOnly, PutOnly = VwapSideGate.SidePutOnly;
    private const string Call = TradingConst.SignalType.CALL, Put = TradingConst.SignalType.PUT;

    [Fact]
    public void Call_LtpAboveVwap_Proceeds() => Assert.Null(VwapSideGate.Evaluate(Call, 55020m, 55000m, Both));

    [Theory]
    [InlineData(54990)]   // below
    [InlineData(55000)]   // equal blocks: CALL needs strictly above
    public void Call_LtpNotAboveVwap_Blocks(double ltp)
    {
        var reason = VwapSideGate.Evaluate(Call, (decimal)ltp, 55000m, Both);

        Assert.NotNull(reason);
        Assert.Contains("CALL needs LTP > VWAP", reason);
    }

    [Fact]
    public void Put_LtpBelowVwap_Proceeds() => Assert.Null(VwapSideGate.Evaluate(Put, 54990m, 55000m, Both));

    [Theory]
    [InlineData(55020)]   // above
    [InlineData(55000)]   // equal blocks: PUT needs strictly below
    public void Put_LtpNotBelowVwap_Blocks(double ltp)
    {
        var reason = VwapSideGate.Evaluate(Put, (decimal)ltp, 55000m, Both);

        Assert.NotNull(reason);
        Assert.Contains("PUT needs LTP < VWAP", reason);
    }

    [Fact]
    public void SmallestStep_IsHonoured()
    {
        Assert.Null(VwapSideGate.Evaluate(Call, 55000.01m, 55000m, Both));
        Assert.NotNull(VwapSideGate.Evaluate(Call, 54999.99m, 55000m, Both));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void NonPositive_Vwap_FailsOpen(double vwap)
    {
        Assert.Null(VwapSideGate.Evaluate(Call, 55020m, (decimal)vwap, Both));
        Assert.Null(VwapSideGate.Evaluate(Put, 54990m, (decimal)vwap, Both));
    }

    [Fact]
    public void Null_Vwap_FailsOpen()
    {
        Assert.Null(VwapSideGate.Evaluate(Call, 55020m, null, Both));
        Assert.Null(VwapSideGate.Evaluate(Put, 54990m, null, Both));
    }

    [Fact]
    public void Unavailable_Ltp_FailsOpen() => Assert.Null(VwapSideGate.Evaluate(Call, 0m, 55000m, Both));

    [Theory]
    [InlineData(Both, "CALL", true)]
    [InlineData(Both, "PUT", true)]
    [InlineData(CallOnly, "CALL", true)]
    [InlineData(CallOnly, "PUT", false)]
    [InlineData(PutOnly, "PUT", true)]
    [InlineData(PutOnly, "CALL", false)]
    public void Side_ScopesTheGate(int side, string signalType, bool applies)
        => Assert.Equal(applies, VwapSideGate.AppliesTo(side, signalType));

    [Fact]
    public void Side_OutOfScopeDirection_Proceeds()
    {
        Assert.Null(VwapSideGate.Evaluate(Put, 55020m, 55000m, CallOnly));   // PUT on the wrong side, but CALL-only
        Assert.Null(VwapSideGate.Evaluate(Call, 54990m, 55000m, PutOnly));
    }

    // ── wiring (all other dependencies null: the gate must not touch them) ───────────────────

    [Fact]
    public async Task DisabledByDefault_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService(), Call, 54990m, 55000m));

    [Fact]
    public async Task Enabled_DefaultSideIsBoth()
    {
        var settings = new FakeStrategySettingsService().Set(VwapSideGate.EnableKey, 1);

        var call = await Block(settings, Call, 54990m, 55000m);
        Assert.NotNull(call);
        Assert.Contains("(BANKNIFTY)", call);
        Assert.NotNull(await Block(settings, Put, 55020m, 55000m));
        Assert.Null(await Block(settings, Call, 55020m, 55000m));
        Assert.Null(await Block(settings, Put, 54990m, 55000m));
    }

    [Fact]
    public async Task Enabled_CallOnlySide_LeavesPutAlone()
    {
        var settings = new FakeStrategySettingsService().Set(VwapSideGate.EnableKey, 1).Set(VwapSideGate.SideKey, CallOnly);

        Assert.NotNull(await Block(settings, Call, 54990m, 55000m));
        Assert.Null(await Block(settings, Put, 55020m, 55000m));
    }

    [Fact]
    public async Task Enabled_NullVwap_FailsOpen()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(VwapSideGate.EnableKey, 1), Call, 54990m, null));

    [Fact]
    public async Task HeroZero_IsExempt()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(VwapSideGate.EnableKey, 1), Call, 54990m, 55000m,
            TradingConst.StrategyType.HeroZero));

    private static Task<string?> Block(FakeStrategySettingsService settings, string signalType, decimal ltp, decimal? vwap,
        string strategyType = TradingConst.StrategyType.Single) =>
        new VwapSideGate().BlockAsync(Context(settings), Snapshot() with { Ltp = ltp, Vwap = vwap },
            new Signal { Symbol = "BANKNIFTY", SignalType = signalType, StrategyType = strategyType }, CancellationToken.None);

    private static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: "BANKNIFTY",
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings(), EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);

    private static MarketSnapshot Snapshot() => new(
        Candles1m: new List<Candle>(),
        Candles5m: new List<Candle>(),
        OptionChain: new List<OptionChainData>(),
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Uptrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: 48m, Ltp: 55000m, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);
}
