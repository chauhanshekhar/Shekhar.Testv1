using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// EarlyTrendGate: take the 1m-trend direction only while the 5m trend has not yet turned the same way (buy the turn).
/// Backtest to 2026-10-07: early CALLs stopped first 36-40% of the time vs about 50% for late ones. See the gate's summary.
/// </summary>
public class EarlyTrendGateTests
{
    private const int Both = EarlyTrendGate.SideBoth, CallOnly = EarlyTrendGate.SideCallOnly, PutOnly = EarlyTrendGate.SidePutOnly;
    private const string Call = TradingConst.SignalType.CALL, Put = TradingConst.SignalType.PUT;

    [Fact]
    public void Call_5mTrendAlreadyUp_Blocks()
    {
        var reason = EarlyTrendGate.Evaluate(Call, 55020m, 55000m, CallOnly);

        Assert.NotNull(reason);
        Assert.Contains("5m trend already up", reason);
    }

    [Theory]
    [InlineData(54990, 55000)]   // 5m trend down: early, allowed
    [InlineData(55000, 55000)]   // flat: not yet up
    public void Call_5mTrendNotUp_Proceeds(double ema20, double ema50)
        => Assert.Null(EarlyTrendGate.Evaluate(Call, (decimal)ema20, (decimal)ema50, CallOnly));

    [Fact]
    public void Put_BlockedOnlyWhen5mTrendAlreadyDown_AndSideIncludesPut()
    {
        Assert.Null(EarlyTrendGate.Evaluate(Put, 54990m, 55000m, CallOnly));         // default side: PUT never gated
        Assert.NotNull(EarlyTrendGate.Evaluate(Put, 54990m, 55000m, Both));
        Assert.NotNull(EarlyTrendGate.Evaluate(Put, 54990m, 55000m, PutOnly));
        Assert.Null(EarlyTrendGate.Evaluate(Put, 55020m, 55000m, Both));            // 5m not down yet: early PUT allowed
    }

    [Theory]
    [InlineData(0, 55000)]
    [InlineData(55020, 0)]
    public void Unavailable5mEmas_FailOpen(double ema20, double ema50)
        => Assert.Null(EarlyTrendGate.Evaluate(Call, (decimal)ema20, (decimal)ema50, Both));

    [Theory]
    [InlineData(Both, "CALL", true)]
    [InlineData(Both, "PUT", true)]
    [InlineData(CallOnly, "CALL", true)]
    [InlineData(CallOnly, "PUT", false)]
    [InlineData(PutOnly, "PUT", true)]
    [InlineData(PutOnly, "CALL", false)]
    public void Side_ScopesTheGate(int side, string signalType, bool applies)
        => Assert.Equal(applies, EarlyTrendGate.AppliesTo(side, signalType));

    // ── wiring (all other dependencies null: the gate must not touch them) ───────────────────

    [Fact]
    public async Task DisabledByDefault_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService(), Call, 55020m, 55000m));

    [Fact]
    public async Task Enabled_DefaultSideIsCallOnly()
    {
        var settings = new FakeStrategySettingsService().Set(EarlyTrendGate.EnableKey, 1);

        var call = await Block(settings, Call, 55020m, 55000m);
        Assert.NotNull(call);
        Assert.Contains("(BANKNIFTY)", call);
        Assert.Null(await Block(settings, Put, 54990m, 55000m));
    }

    [Fact]
    public async Task HeroZero_IsExempt()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(EarlyTrendGate.EnableKey, 1), Call, 55020m, 55000m,
            TradingConst.StrategyType.HeroZero));

    private static Task<string?> Block(FakeStrategySettingsService settings, string signalType, decimal ema20, decimal ema50,
        string strategyType = TradingConst.StrategyType.Single) =>
        new EarlyTrendGate().BlockAsync(Context(settings), Snapshot() with { Ema20_5m = ema20, Ema50_5m = ema50 },
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


public class EarlyTrendGapUpMorningExceptionTests
{
    private const string Call = TradingConst.SignalType.CALL, Put = TradingConst.SignalType.PUT;
    private static readonly TimeSpan T0959 = new(9, 59, 59), T1000 = new(10, 0, 0), T1001 = new(10, 1, 0), T0925 = new(9, 25, 0);

    private static bool Exempt(string type = Call, decimal e20 = 55020m, decimal e50 = 55000m, decimal? gap = 0.40m,
        TimeSpan? time = null, bool on = true)
        => EarlyTrendGate.GapUpMorningExempt(type, e20, e50, gap, time ?? T0925, on);

    [Fact]
    public void BeforeTen_GapUpCall_IsAllowed() => Assert.True(Exempt(time: T0959));

    [Fact]
    public void ExactlyTen_IsBlocked() => Assert.False(Exempt(time: T1000));

    [Fact]
    public void AfterTen_IsBlocked() => Assert.False(Exempt(time: T1001));

    [Fact]
    public void SwitchOff_NeverExempt() => Assert.False(Exempt(on: false));

    [Theory]
    [InlineData(0.29)]    // just under the fixed 0.30%
    [InlineData(0.0)]
    [InlineData(-0.50)]   // gap down
    public void NormalOrGapDownOpen_IsBlocked(double gap) => Assert.False(Exempt(gap: (decimal)gap));

    [Fact]
    public void ExactlyThirtyBasisPoints_IsGapUp() => Assert.True(Exempt(gap: 0.30m));

    [Fact]
    public void GapUpPut_IsNotExempt() => Assert.False(Exempt(type: Put));

    [Fact]
    public void Missing5mEma_IsNotExempt()
    {
        Assert.False(Exempt(e20: 0m));
        Assert.False(Exempt(e50: 0m));
    }

    [Fact]
    public void FiveMinuteTrendNotUp_IsNotExempt() => Assert.False(Exempt(e20: 54990m, e50: 55000m));

    [Theory]
    [InlineData(0, 55000)]
    [InlineData(55200, 0)]
    [InlineData(-1, 55000)]
    [InlineData(55200, -1)]
    public void GapPct_MissingZeroOrNegativePrices_IsNull(double open, double prevClose)
        => Assert.Null(EarlyTrendGate.GapPct((decimal)open, (decimal)prevClose));

    [Fact]
    public void GapPct_UsesOpenOverPreviousClose() => Assert.Equal(0.40m, Math.Round(EarlyTrendGate.GapPct(55220m, 55000m)!.Value, 2));

    [Fact]
    public void InvalidGap_IsNotExempt() => Assert.False(Exempt(gap: EarlyTrendGate.GapPct(0m, 55000m)));

    // The gate's own decision and the side setting are untouched by the exception.
    [Fact]
    public void ExistingDecision_Unchanged()
    {
        Assert.NotNull(EarlyTrendGate.Evaluate(Call, 55020m, 55000m, EarlyTrendGate.SideCallOnly));
        Assert.Null(EarlyTrendGate.Evaluate(Put, 54990m, 55000m, EarlyTrendGate.SideCallOnly));
    }

    // ── wiring: other dependencies null, so the gate sees no paper mode and no logger ──────────────────
    private static Task<string?> Block(FakeStrategySettingsService settings, string strategyType = TradingConst.StrategyType.Single)
        => new EarlyTrendGate().BlockAsync(
            new SignalContext(
                Symbol: "BANKNIFTY",
                CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
                SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
                Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
                HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
                Settings: new TradingSettings(), EventCalendar: null!, Instruments: null!,
                SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings),
            new MarketSnapshot(
                Candles1m: new List<Candle>(), Candles5m: new List<Candle>(), OptionChain: new List<OptionChainData>(),
                PrevOiByKey: new Dictionary<(decimal, string), long>(),
                MarketRegime: new MarketRegime(MarketTrend.Uptrend, MarketVolatility.High, false, false, false, 0m),
                Ema20: 0m, Ema50: 0m, Ema20_5m: 55020m, Ema50_5m: 55000m,
                Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: 48m, Ltp: 55000m, YesterdayClose: 55000m,
                CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
                DayOpen: 55220m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
                StrategyHint: "", NewsEvent: null,
                LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null),
            new Signal { Symbol = "BANKNIFTY", SignalType = Call, StrategyType = strategyType }, CancellationToken.None);

    // The old "switch on but not paper mode still blocks" wiring test was removed with the paper-only rule (2026-11-05, user-approved):
    // the gate reads IstClock.Now, so a switch-on wiring test would depend on the wall clock. The decision is covered by the pure tests above.

    [Fact]
    public async Task SwitchOffByDefault_Blocks()
        => Assert.NotNull(await Block(new FakeStrategySettingsService().Set(EarlyTrendGate.EnableKey, 1)));

    [Fact]
    public async Task HeroZero_StaysExempt_WhateverTheSwitch()
        => Assert.Null(await Block(new FakeStrategySettingsService()
            .Set(EarlyTrendGate.EnableKey, 1).Set(EarlyTrendGate.GapUpMorningKey, 1), TradingConst.StrategyType.HeroZero));
}
