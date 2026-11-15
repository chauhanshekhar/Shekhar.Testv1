using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The floor blocks directional signals whose index stop has collapsed to the
/// MinimumIndexStopPoints clamp, and deliberately exempts HeroZero. Both halves are pinned:
/// the exemption exists because every HERO_ZERO signal on record was generated below the floor,
/// so enforcing it there would retire a working strategy rather than protect anything.
/// </summary>
public class IndexStopFloorGateTests
{
    private const string Symbol = "NIFTY";

    private static SignalContext Context(FakeStrategySettingsService settings, TradingSettings? trading = null) => new(
        Symbol: Symbol,
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: trading ?? new TradingSettings(), EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);

    private static MarketSnapshot Snapshot(decimal atrRaw) => new(
        Candles1m: new List<Candle>(),
        Candles5m: new List<Candle>(),
        OptionChain: new List<OptionChainData>(),
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Downtrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: atrRaw, Ltp: 0m, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);

    private static Task<string?> Block(decimal atrRaw, string strategyType, FakeStrategySettingsService? settings = null,
        TradingSettings? trading = null) =>
        new IndexStopFloorGate().BlockAsync(
            Context(settings ?? new FakeStrategySettingsService(), trading),
            Snapshot(atrRaw),
            new Signal { Symbol = Symbol, StrategyType = strategyType },
            CancellationToken.None);

    // Defaults: 20 / 1.5 = 13.33.

    [Theory]
    [InlineData("9.9795")]   // the observed NIFTY average regime
    [InlineData("13.32")]
    [InlineData("0.00")]
    public async Task Directional_BelowFloor_Blocks(string atrRaw)
    {
        var block = await Block(
            decimal.Parse(atrRaw, System.Globalization.CultureInfo.InvariantCulture),
            TradingConst.StrategyType.Single);

        Assert.NotNull(block);
        Assert.Contains("index stop floor", block);
    }

    [Theory]
    [InlineData("13.34")]
    [InlineData("40.00")]
    public async Task Directional_AtOrAboveFloor_Passes(string atrRaw)
    {
        Assert.Null(await Block(
            decimal.Parse(atrRaw, System.Globalization.CultureInfo.InvariantCulture),
            TradingConst.StrategyType.Single));
    }

    [Theory]
    [InlineData("9.9795")]   // signal 60, 2026-09-15 — a real HERO_ZERO entry below the floor
    [InlineData("0.00")]
    public async Task HeroZero_IsExempt_EvenFarBelowFloor(string atrRaw)
    {
        Assert.Null(await Block(
            decimal.Parse(atrRaw, System.Globalization.CultureInfo.InvariantCulture),
            TradingConst.StrategyType.HeroZero));
    }

    [Fact]
    public async Task CanBeDisabledEntirely()
    {
        var settings = new FakeStrategySettingsService().Set("EnableIndexStopFloor", 0m);

        Assert.Null(await Block(6.79m, TradingConst.StrategyType.Single, settings));
    }

    // ── Floor decoupled from the stop (2026-10-07, docs/research/2026-10-07-stop-width) ──────────

    [Fact]
    public void FloorMultiplierUnset_FollowsTheStopMultiplier_AsBefore()
    {
        Assert.Equal(20m / 1.5m, IndexStopFloorGate.MinAtrRaw(new TradingSettings()));
        Assert.Equal(16.00m, IndexStopFloorGate.MinAtrRaw(new TradingSettings { AtrStopLossMultiplier = 1.25m }));
    }

    [Fact]
    public void FloorMultiplierSet_KeepsTheFloorWhenTheStopTightens()
        => Assert.Equal(20m / 1.5m, IndexStopFloorGate.MinAtrRaw(
            new TradingSettings { AtrStopLossMultiplier = 1.25m, IndexStopFloorAtrMultiplier = 1.5m }));

    [Fact]
    public async Task Signal146_Atr14_PassesAtA125Stop_OnlyWithTheFloorKept()
    {
        // 2026-10-07 signal 146 (NIFTY CALL, AtrRaw 14.0, +2,509): the coupled 1.25 floor (16.00) would block it.
        var coupled = new TradingSettings { AtrStopLossMultiplier = 1.25m };
        var kept = new TradingSettings { AtrStopLossMultiplier = 1.25m, IndexStopFloorAtrMultiplier = 1.5m };

        Assert.NotNull(await Block(14.00m, TradingConst.StrategyType.Single, trading: coupled));
        Assert.Null(await Block(14.00m, TradingConst.StrategyType.Single, trading: kept));
        Assert.NotNull(await Block(13.32m, TradingConst.StrategyType.Single, trading: kept));
    }
}
