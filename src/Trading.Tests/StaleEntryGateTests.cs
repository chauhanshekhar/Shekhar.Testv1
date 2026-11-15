using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// StaleEntryGate blocks an entry whose LIVE index is already a fraction of the risk against the
/// candle-close entry. Regression cover for 2026-10-01 signal 115 (BANKNIFTY PUT): the candle
/// entry was 54413.40 with the index stop at 54486.04, but live BANKNIFTY was already 54487.10 -
/// INDEX_SL_HIT 4 s after the fill.
/// </summary>
public class StaleEntryGateTests
{
    private const decimal MaxR = StaleEntryGate.DefaultMaxAdverseR;   // 0.25

    // ── Decision ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Signal115_LiveAlreadyAtTheStop_Blocks()
    {
        var reason = StaleEntryGate.Evaluate(TradingConst.SignalType.PUT, 54413.40m, 54486.04m, 54487.10m, MaxR);

        Assert.NotNull(reason);
        Assert.Contains("Stale entry", reason);
    }

    [Fact]
    public void Put_LiveMovedInFavour_Proceeds()
        => Assert.Null(StaleEntryGate.Evaluate(TradingConst.SignalType.PUT, 72068.49m, 72115.43m, 72031.20m, MaxR));

    [Fact]
    public void Call_LiveAgainstByMoreThanTheLimit_Blocks()
        // risk 40, live 16 below entry = 0.40R
        => Assert.NotNull(StaleEntryGate.Evaluate(TradingConst.SignalType.CALL, 55000m, 54960m, 54984m, MaxR));

    [Fact]
    public void Call_LiveAgainstWithinTheLimit_Proceeds()
        // risk 40, live 8 below entry = 0.20R
        => Assert.Null(StaleEntryGate.Evaluate(TradingConst.SignalType.CALL, 55000m, 54960m, 54992m, MaxR));

    [Fact]
    public void ExactlyAtTheLimit_Blocks()
        // risk 40, live 10 against = 0.25R - inclusive
        => Assert.NotNull(StaleEntryGate.Evaluate(TradingConst.SignalType.PUT, 55000m, 55040m, 55010m, MaxR));

    [Fact]
    public void Signal104_LiveAt029R_Blocks()
        // 2026-10-01 09:30: SENSEX PUT, entry 72301.53, stop 72381.88, live 72325.0 = 0.29R. Lost -1,580 at its index
        // stop; 0.35R would have let it through, 0.25R blocks it.
        => Assert.NotNull(StaleEntryGate.Evaluate(TradingConst.SignalType.PUT, 72301.53m, 72381.88m, 72325.0m, MaxR));

    [Fact]
    public void Signal121_LiveAt021R_Proceeds()
        // 2026-10-01 14:20: BANKNIFTY PUT, entry 54169.60, stop 54228.28, live 54182.2 = 0.21R - below the limit.
        => Assert.Null(StaleEntryGate.Evaluate(TradingConst.SignalType.PUT, 54169.60m, 54228.28m, 54182.2m, MaxR));

    [Fact]
    public void Measure_Put_LiveAboveEntryIsAdverse()
    {
        var m = StaleEntryGate.Measure(TradingConst.SignalType.PUT, 55000m, 55040m, 55010m);

        Assert.Equal((40m, 10m, 0.25m), m);
    }

    [Fact]
    public void Measure_Call_LiveBelowEntryIsAdverse_AndFavourableIsNegative()
    {
        Assert.Equal((40m, 10m, 0.25m), StaleEntryGate.Measure(TradingConst.SignalType.CALL, 55000m, 54960m, 54990m));
        Assert.Equal(-0.25m, StaleEntryGate.Measure(TradingConst.SignalType.CALL, 55000m, 54960m, 55010m)!.Value.AdverseR);
    }

    [Theory]
    [InlineData(55000, 55000, 55100)]   // zero risk
    [InlineData(55000, 55040, 0)]       // no live price
    public void Measure_UndefinedInputs_ReturnNull(double entry, double stop, double live)
        => Assert.Null(StaleEntryGate.Measure(TradingConst.SignalType.PUT, (decimal)entry, (decimal)stop, (decimal)live));

    [Fact]
    public void Signal124_DependsOnWhichTickTheGateReads()
    {
        // 2026-10-05 12:10:05 BANKNIFTY PUT, entry 54406.4, stop 54471.6 (65.2 pts). The 12:09:58 tick (54422.1) is
        // 0.24R - passes; the 12:10:03 tick (54426.0) is 0.30R - blocks. The gate logs the tick it used so this is visible.
        Assert.Null(StaleEntryGate.Evaluate(TradingConst.SignalType.PUT, 54406.4m, 54471.6m, 54422.1m, MaxR));
        Assert.NotNull(StaleEntryGate.Evaluate(TradingConst.SignalType.PUT, 54406.4m, 54471.6m, 54426.0m, MaxR));
    }

    // ── hold until next candle ───────────────────────────────────────────────────────────────

    [Fact]
    public void Hold_SameEntryPrice_WithinWindow_StaysHeld()
        => Assert.True(StaleEntryGate.IsStillHeld(54406.4m, At(12, 10, 0), 54406.4m, At(12, 10, 40)));

    [Fact]
    public void Hold_NewCandle_NewEntryPrice_IsReleased()
        => Assert.False(StaleEntryGate.IsStillHeld(54406.4m, At(12, 10, 0), 54431.0m, At(12, 11, 3)));

    [Fact]
    public void Hold_ExpiresAfterTwoMinutes_EvenWithSamePrice()
        => Assert.False(StaleEntryGate.IsStillHeld(54406.4m, At(12, 10, 0), 54406.4m, At(12, 12, 1)));

    [Fact]
    public async Task Hold_BlocksTheNextSecond_WithoutReadingAnyTick()
    {
        // 2026-10-06 signal 137: blocked at 13:05:18, fired at 13:05:19 on a calmer tick of the same candle. With the hold the
        // second evaluation returns the block straight from memory (TickRepo is null here: reading a tick would throw).
        StaleEntryGate.ClearHeld();
        StaleEntryGate.SeedHeld("BANKNIFTY", TradingConst.SignalType.PUT, 54406.4m, Trading.Domain.IstClock.Now);
        var settings = new FakeStrategySettingsService().Set(StaleEntryGate.EnableKey, 1);

        var reason = await new StaleEntryGate().BlockAsync(Context(settings), Snapshot(), Sig(), CancellationToken.None);
        StaleEntryGate.ClearHeld();

        Assert.NotNull(reason);
        Assert.Contains("held until the next 1m candle", reason);
        Assert.Contains("(BANKNIFTY)", reason);
    }

    private static DateTime At(int h, int m, int s) => new(2026, 10, 6, h, m, s);

    private static Signal Sig() => new()
    {
        Symbol = "BANKNIFTY", StrategyType = TradingConst.StrategyType.Single, SignalType = TradingConst.SignalType.PUT,
        EntryPrice = 54406.4m, StopLoss = 54471.6m,
    };

    [Theory]
    [InlineData(55000, 55000, 55100, 0.25)]   // zero risk
    [InlineData(55000, 55040, 0, 0.25)]       // no live price
    [InlineData(55000, 55040, 55100, 0)]      // limit switched to 0
    public void DegenerateInputs_Proceed(double entry, double stop, double live, double maxR)
        => Assert.Null(StaleEntryGate.Evaluate(TradingConst.SignalType.PUT,
            (decimal)entry, (decimal)stop, (decimal)live, (decimal)maxR));

    // ── Gate wiring (TickRepo is null: any tick query would throw) ───────────────────────────

    [Fact]
    public async Task DisabledByDefault_ProceedsWithoutQueryingTicks()
        => Assert.Null(await Block(new FakeStrategySettingsService(), TradingConst.StrategyType.Single));

    [Fact]
    public async Task HeroZero_IsExempt()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(StaleEntryGate.EnableKey, 1),
            TradingConst.StrategyType.HeroZero));

    private const string Symbol = "BANKNIFTY";

    private static Task<string?> Block(FakeStrategySettingsService settings, string strategyType) =>
        new StaleEntryGate().BlockAsync(
            Context(settings),
            Snapshot(),
            new Signal
            {
                Symbol = Symbol, StrategyType = strategyType, SignalType = TradingConst.SignalType.PUT,
                EntryPrice = 54413.40m, StopLoss = 54486.04m,
            },
            CancellationToken.None);

    private static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: Symbol,
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
        MarketRegime: new MarketRegime(MarketTrend.Downtrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: 48m, Ltp: 54413.40m, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);
}
