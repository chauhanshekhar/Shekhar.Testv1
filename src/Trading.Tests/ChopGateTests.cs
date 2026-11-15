using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>ChopGate: any symbol, off by default, mode-agnostic. chop = sum(1m ranges, 15 bars) / (15-bar high-low).</summary>
public class ChopGateTests
{
    /// <summary>n bars of the given range; bar i shifted up by i*step, so span = (n-1)*step + range.</summary>
    private static List<Candle> Bars(int n, decimal range, decimal step)
    {
        var list = new List<Candle>();
        for (var i = 0; i < n; i++)
            list.Add(new Candle { Symbol = "SENSEX_1m", Low = 80000m + i * step, High = 80000m + i * step + range });
        return list;
    }

    /// <summary>15 bars of the given range (sum = 15 * range); the last bar is shifted so the total high-low span is exactly <paramref name="span"/>.</summary>
    private static List<Candle> WithSpan(decimal range, decimal span)
    {
        var list = new List<Candle>();
        for (var i = 0; i < 14; i++) list.Add(new Candle { Low = 80000m, High = 80000m + range });
        list.Add(new Candle { Low = 80000m + span - range, High = 80000m + span });
        return list;
    }

    private static List<Candle> ChopOf(decimal chop) => WithSpan(10m, 150m / chop);   // approximate; use WithSpan for exact boundaries

    [Fact]
    public void Chop_IsSumOfRangesOverSpan()
    {
        Assert.Equal(15m, ChopGate.Chop(Bars(15, 10m, 0m)));   // sum 150, span 10
        Assert.Equal(1m, ChopGate.Chop(Bars(15, 10m, 10m)));   // sum 150, span 150: one-way
    }

    [Fact]
    public void Chop_UsesOnlyLast15Bars()
    {
        var bars = Bars(20, 10m, 10m);
        bars[0].High = 90000m;   // outside the window: ignored
        Assert.Equal(1m, ChopGate.Chop(bars));
    }

    [Fact]
    public void Chop_FewerThan15Bars_OrZeroSpan_IsNull()
    {
        Assert.Null(ChopGate.Chop(Bars(14, 10m, 0m)));
        Assert.Null(ChopGate.Chop(Bars(15, 0m, 0m)));
    }

    [Theory]
    [InlineData(9, 30, false)]    // chop = 135 / 30 = 4.5 exactly: strictly greater than 4.5 blocks, so this passes
    [InlineData(9, 29, true)]     // 135 / 29 = 4.66
    [InlineData(9, 31, false)]    // 135 / 31 = 4.35
    public async Task Threshold_IsStrictlyAbove4_5(int range, int span, bool blocks)
        => Assert.Equal(blocks, await Block(On(), "SENSEX", WithSpan(range, span)) is not null);

    [Fact]
    public async Task Message_NamesGateAndSymbol()
    {
        var reason = await Block(On(), "SENSEX", ChopOf(6m));
        Assert.Contains("Chop gate", reason);
        Assert.Contains("(SENSEX)", reason);
    }

    [Fact]
    public async Task DisabledByDefault_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService(), "SENSEX", ChopOf(6m)));

    [Fact]
    public async Task SwitchZero_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(ChopGate.EnableKey, 0), "SENSEX", ChopOf(6m)));

    [Theory]
    [InlineData("NIFTY")]
    [InlineData("BANKNIFTY")]
    public async Task OtherSymbols_BlockWhenEnabled(string symbol)
        => Assert.NotNull(await Block(On(), symbol, ChopOf(6m)));

    [Theory]
    [InlineData("NIFTY")]
    [InlineData("BANKNIFTY")]
    public async Task OtherSymbols_ProceedWhenOff(string symbol)
        => Assert.Null(await Block(new FakeStrategySettingsService(), symbol, ChopOf(6m)));

    [Fact]
    public async Task HeroZero_IsExempt()
        => Assert.Null(await Block(On(), "SENSEX", ChopOf(6m), TradingConst.StrategyType.HeroZero));

    [Fact]
    public async Task TooFewBars_FailsOpen()
        => Assert.Null(await Block(On(), "SENSEX", Bars(10, 10m, 0m)));

    // Orders is null here: the decision never reads the trading mode, so paper and live behave the same.
    [Fact]
    public async Task Blocks_WithoutReadingTradingMode()
        => Assert.NotNull(await Block(On(), "SENSEX", ChopOf(6m)));

    private static FakeStrategySettingsService On() => new FakeStrategySettingsService().Set(ChopGate.EnableKey, 1);

    private static Task<string?> Block(FakeStrategySettingsService settings, string symbol, List<Candle> candles,
        string strategyType = TradingConst.StrategyType.Single) =>
        new ChopGate().BlockAsync(Context(settings, symbol), Snapshot(candles),
            new Signal { Symbol = symbol, SignalType = TradingConst.SignalType.PUT, StrategyType = strategyType }, CancellationToken.None);

    private static SignalContext Context(FakeStrategySettingsService settings, string symbol) => new(
        Symbol: symbol,
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings(), EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);

    private static MarketSnapshot Snapshot(List<Candle> candles) => new(
        Candles1m: candles,
        Candles5m: new List<Candle>(),
        OptionChain: new List<OptionChainData>(),
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Uptrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: 48m, Ltp: 80000m, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);
}
