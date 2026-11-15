using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// FastEmaGate: block a CALL while the 1m EMA9 is not above EMA21, by default only on gap-up sessions. Off by default.
/// Evidence is thin (3 stop-outs removed in the backtest, holdout unchanged), so the gate must never act unless enabled.
/// </summary>
public class FastEmaGateTests
{
    private const string Call = TradingConst.SignalType.CALL, Put = TradingConst.SignalType.PUT;

    [Fact]
    public void Call_Ema9NotAbove21_Blocks()
    {
        var reason = FastEmaGate.Evaluate(Call, 55000m, 55010m, FastEmaGate.SideCallOnly);
        Assert.NotNull(reason);
        Assert.Contains("not above", reason);
    }

    [Fact]
    public void Call_Ema9Above21_Proceeds()
        => Assert.Null(FastEmaGate.Evaluate(Call, 55020m, 55000m, FastEmaGate.SideCallOnly));

    [Fact]
    public void Call_Equal_Blocks()
        => Assert.NotNull(FastEmaGate.Evaluate(Call, 55000m, 55000m, FastEmaGate.SideCallOnly));

    [Fact]
    public void Put_GatedOnlyWhenSideIncludesPut()
    {
        Assert.Null(FastEmaGate.Evaluate(Put, 55020m, 55000m, FastEmaGate.SideCallOnly));
        Assert.NotNull(FastEmaGate.Evaluate(Put, 55020m, 55000m, FastEmaGate.SideBoth));
        Assert.Null(FastEmaGate.Evaluate(Put, 54990m, 55000m, FastEmaGate.SideBoth));
    }

    [Theory]
    [InlineData(0, 55000)]
    [InlineData(55000, 0)]
    public void UnavailableEmas_FailOpen(double ema9, double ema21)
        => Assert.Null(FastEmaGate.Evaluate(Call, (decimal)ema9, (decimal)ema21, FastEmaGate.SideBoth));

    [Theory]
    [InlineData(55200, 55000, true)]    // +0.36%
    [InlineData(55100, 55000, false)]   // +0.18%
    [InlineData(54900, 55000, false)]   // gap down
    [InlineData(0, 55000, false)]
    [InlineData(55200, 0, false)]
    public void OpenedGapUp_UsesTheConfiguredThreshold(double open, double prevClose, bool expected)
        => Assert.Equal(expected, FastEmaGate.OpenedGapUp((decimal)open, (decimal)prevClose, 0.30m));

    // ── wiring (all other dependencies null: the gate must not touch them) ───────────────────

    [Fact]
    public async Task DisabledByDefault_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService(), Call, 55200m, 55000m));

    [Fact]
    public async Task Enabled_BlocksGapUpCall_ButNotNormalOpen()
    {
        var settings = new FakeStrategySettingsService().Set(FastEmaGate.EnableKey, 1);

        var gapUp = await Block(settings, Call, 55200m, 55000m);
        Assert.NotNull(gapUp);
        Assert.Contains("(BANKNIFTY)", gapUp);
        Assert.Null(await Block(settings, Call, 55050m, 55000m));   // normal open: out of scope
    }

    [Fact]
    public async Task GapUpOnlyOff_AppliesOnEveryOpen()
    {
        var settings = new FakeStrategySettingsService().Set(FastEmaGate.EnableKey, 1).Set(FastEmaGate.GapUpOnlyKey, 0);
        Assert.NotNull(await Block(settings, Call, 55050m, 55000m));
    }

    [Fact]
    public async Task HeroZero_IsExempt()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(FastEmaGate.EnableKey, 1), Call, 55200m, 55000m,
            TradingConst.StrategyType.HeroZero));

    /// <summary>EMA9 55000 below EMA21 55010, so an enabled gate in scope always has a reason to block a CALL.</summary>
    private static Task<string?> Block(FakeStrategySettingsService settings, string signalType, decimal dayOpen, decimal yesterdayClose,
        string strategyType = TradingConst.StrategyType.Single) =>
        new FastEmaGate().BlockAsync(Context(settings),
            Snapshot() with { DayOpen = dayOpen, YesterdayClose = yesterdayClose, Ema9 = 55000m, Ema21 = 55010m },
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
