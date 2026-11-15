using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// EnableOiConfirmationGate: per-symbol switch, default ON. With no row the gate behaves exactly as
/// before; with 0 it lets a built setup through without waiting for a 1m candle to confirm. On
/// 2026-10-05 the gate held setups 10-107 min and its rejected setups replayed as profitably as the
/// passed ones (docs/research/2026-10-05-late-entry), so it is switchable for a paper test.
/// </summary>
public class OiConfirmationGateTests
{
    private const string Symbol = "SENSEX";
    private const decimal Spot = 72_300m;

    private sealed class OneInstrumentCache : IInstrumentCache
    {
        public IReadOnlyList<TradingInstrument> GetActive() =>
            [new TradingInstrument { Symbol = Symbol, StrikeInterval = 100, LotSize = 20, IsActive = true }];
        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: Symbol,
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: new OiAnalysisService(), Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings(), EventCalendar: null!, Instruments: new OneInstrumentCache(),
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);

    /// <summary>ATM PE with the given OI change; the last 1m candle moves by <paramref name="lastCandleMove"/>.</summary>
    private static MarketSnapshot Snapshot(long peOiChange, decimal lastCandleMove, bool emptyChain = false) => new(
        Candles1m:
        [
            new Candle { Symbol = Symbol, Close = Spot, CandleTime = DateTime.UtcNow.AddMinutes(-2) },
            new Candle { Symbol = Symbol, Close = Spot + lastCandleMove, CandleTime = DateTime.UtcNow.AddMinutes(-1) },
        ],
        Candles5m: new List<Candle>(),
        OptionChain: emptyChain
            ? new List<OptionChainData>()
            :
            [
                new OptionChainData { Symbol = Symbol, TradingSymbol = "SENSEXTEST72300PE", StrikePrice = Spot,
                                      OptionType = TradingConst.OptionType.PE, OpenInterest = 100_000,
                                      ChangeInOi = peOiChange, UnderlyingLtp = Spot },
            ],
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Downtrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: 0.05m, AtrRaw: 30m, Ltp: Spot, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);

    private static readonly Signal Put = new()
    {
        Symbol = Symbol, SignalType = TradingConst.SignalType.PUT, OptionType = TradingConst.OptionType.PE,
    };

    private static Task<string?> Block(FakeStrategySettingsService settings, MarketSnapshot market) =>
        new OiConfirmationGate().BlockAsync(Context(settings), market, Put, CancellationToken.None);

    // PUT not confirmed: the last candle ROSE while PE OI rose (no long build-up / short covering for the PUT).
    private static MarketSnapshot NotConfirmed() => Snapshot(peOiChange: 5_000, lastCandleMove: +10m);

    [Fact]
    public async Task NoSettingRow_BlocksAsBefore()
    {
        var reason = await Block(new FakeStrategySettingsService(), NotConfirmed());

        Assert.NotNull(reason);
        Assert.Contains("OI does not confirm", reason);
    }

    [Fact]
    public async Task SwitchOn_Blocks()
        => Assert.NotNull(await Block(new FakeStrategySettingsService().Set(OiConfirmationGate.EnableKey, 1), NotConfirmed()));

    [Fact]
    public async Task SwitchOff_LetsTheSetupThrough()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(OiConfirmationGate.EnableKey, 0), NotConfirmed()));

    [Fact]
    public async Task SwitchOff_EmptyChain_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(OiConfirmationGate.EnableKey, 0),
            Snapshot(peOiChange: 5_000, lastCandleMove: +10m, emptyChain: true)));

    [Fact]
    public async Task SwitchOn_ConfirmedPut_Proceeds()
        // Last candle FELL while PE OI rose: long build-up in puts -> confirmed, existing logic unchanged.
        => Assert.Null(await Block(new FakeStrategySettingsService(), Snapshot(peOiChange: 5_000, lastCandleMove: -10m)));
}
