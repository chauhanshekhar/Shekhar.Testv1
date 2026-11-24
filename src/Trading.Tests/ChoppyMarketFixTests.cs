using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Choppy-market fixes: FiveMinuteBars helpers, CloseBreakoutGate (fix 1), StrategyManager.RiskAtr + StopNoiseGate
/// (fix 2), EfficiencyRatioGate (fix 3) and the re-registered MaxDailyTradesGate switch (fix 4).
/// Every gate is off by default.
/// </summary>
public class ChoppyMarketFixTests
{
    // ── Bar builders ─────────────────────────────────────────────────────────────────────────

    private static Candle Bar(decimal open, decimal high, decimal low, decimal close) =>
        new() { Symbol = "NIFTY_5m", Open = open, High = high, Low = low, Close = close };

    /// <summary>Closes rise by <paramref name="step"/> each bar; every bar has the given range around its close.</summary>
    private static List<Candle> Trend(int n, decimal step, decimal range = 10m, decimal start = 24000m)
    {
        var list = new List<Candle>();
        for (var i = 0; i < n; i++)
        {
            var c = start + i * step;
            list.Add(Bar(c - step, c + range / 2, c - range / 2, c));
        }
        return list;
    }

    /// <summary>Closes alternate +swing / -swing: big path, no net move.</summary>
    private static List<Candle> Chop(int n, decimal swing, decimal start = 24000m)
    {
        var list = new List<Candle>();
        for (var i = 0; i < n; i++)
        {
            var c = start + (i % 2 == 0 ? 0m : swing);
            list.Add(Bar(c, c + swing / 2, c - swing / 2, c));
        }
        return list;
    }

    // ── FiveMinuteBars ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void EfficiencyRatio_StraightLine_IsOne()
        => Assert.Equal(1m, FiveMinuteBars.EfficiencyRatio(Trend(10, 10m), 6));

    [Fact]
    public void EfficiencyRatio_PureChop_IsNearZero()
    {
        var er = FiveMinuteBars.EfficiencyRatio(Chop(10, 35m), 6);
        Assert.NotNull(er);
        Assert.True(er < 0.2m);
    }

    [Fact]
    public void EfficiencyRatio_TooFewBars_IsNull()
        => Assert.Null(FiveMinuteBars.EfficiencyRatio(Trend(6, 10m), 6));   // needs bars + 1

    [Fact]
    public void AverageRange_UsesLastBarsOnly()
    {
        var bars = Trend(10, 5m, range: 20m);
        bars[0] = Bar(1, 1000, 0, 1);   // outside the last 6: ignored
        Assert.Equal(20m, FiveMinuteBars.AverageRange(bars, 6));
    }

    [Fact]
    public void CloseBreakout_CallNeedsCloseAboveHighestHigh()
    {
        var bars = Trend(6, 0m, range: 20m);            // highs 24010
        bars.Add(Bar(24000, 24030, 23995, 24025));      // closes above 24010
        Assert.Equal((true, 24010m), FiveMinuteBars.CloseBreakout(bars, 5, call: true));

        bars[^1] = Bar(24000, 24030, 23995, 24008);     // wick above, close below: not a breakout
        Assert.False(FiveMinuteBars.CloseBreakout(bars, 5, call: true)!.Value.Broke);
    }

    [Fact]
    public void CloseBreakout_PutNeedsCloseBelowLowestLow()
    {
        var bars = Trend(6, 0m, range: 20m);            // lows 23990
        bars.Add(Bar(24000, 24005, 23970, 23975));
        Assert.Equal((true, 23990m), FiveMinuteBars.CloseBreakout(bars, 5, call: false));
    }

    // ── Fix 1: CloseBreakoutGate ─────────────────────────────────────────────────────────────

    [Fact]
    public void CloseBreakoutGate_PassesConfirmedBreakout()
    {
        var bars = Trend(6, 0m, range: 20m);
        bars.Add(Bar(24000, 24030, 23995, 24025));
        Assert.Null(CloseBreakoutGate.Evaluate(bars, ltp: 24020m, lookback: 5, call: true, "NIFTY"));
    }

    [Fact]
    public void CloseBreakoutGate_BlocksWickOnlyBreakout()
    {
        var bars = Trend(6, 0m, range: 20m);
        bars.Add(Bar(24000, 24030, 23995, 24008));
        Assert.Contains("not above", CloseBreakoutGate.Evaluate(bars, 24020m, 5, true, "NIFTY"));
    }

    [Fact]
    public void CloseBreakoutGate_BlocksWhenLtpFellBackInside()
    {
        var bars = Trend(6, 0m, range: 20m);
        bars.Add(Bar(24000, 24030, 23995, 24025));
        Assert.Contains("breakout failed", CloseBreakoutGate.Evaluate(bars, 24005m, 5, true, "NIFTY"));
    }

    [Fact]
    public void CloseBreakoutGate_TooFewBars_FailsClosed()
        => Assert.NotNull(CloseBreakoutGate.Evaluate(Trend(3, 10m), 24100m, 5, true, "NIFTY"));

    // ── Fix 2: StopNoiseGate + RiskAtr ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(20, true)]    // 20-pt stop vs 35-pt average 5m range: inside the noise
    [InlineData(35, false)]   // equal to the range: allowed
    [InlineData(45, false)]
    public void StopNoiseGate_BlocksStopInsideNoise(int stop, bool blocks)
        => Assert.Equal(blocks, StopNoiseGate.Evaluate(Chop(10, 35m), stop, 6, 1.0m, "NIFTY") is not null);

    [Fact]
    public void RiskAtr_OffByDefault_KeepsOneMinuteAtr()
        => Assert.Equal(0m, StrategyManager.RiskAtr(Context(new FakeStrategySettingsService()), Snapshot(Chop(30, 35m)), "NIFTY"));

    [Fact]
    public void RiskAtr_On_IsFiveMinuteAtrPreDividedByStopMultiplier()
    {
        var bars = Chop(30, 35m);
        var settings = new FakeStrategySettingsService().Set("EnableAtr5mStop", 1).Set("Atr5mStopMultiplier", 1.2m);
        var ctx = Context(settings);

        var riskAtr = StrategyManager.RiskAtr(ctx, Snapshot(bars), "NIFTY");

        // AtrRiskService multiplies by AtrStopLossMultiplier, so the effective stop is 1.2 x ATR(5m).
        Assert.Equal(FiveMinuteBars.Atr(bars, 14) * 1.2m, riskAtr * ctx.Settings.AtrStopLossMultiplier, 6);
    }

    // ── Fix 3: EfficiencyRatioGate ───────────────────────────────────────────────────────────

    [Fact]
    public void EfficiencyRatioGate_BlocksChop()
        => Assert.Contains("choppy", EfficiencyRatioGate.Evaluate(Chop(10, 35m), 6, 0.30m, "NIFTY"));

    [Fact]
    public void EfficiencyRatioGate_PassesTrend()
        => Assert.Null(EfficiencyRatioGate.Evaluate(Trend(10, 10m), 6, 0.30m, "NIFTY"));

    [Fact]
    public void EfficiencyRatioGate_TooFewBars_FailsClosed()
        => Assert.NotNull(EfficiencyRatioGate.Evaluate(Trend(3, 10m), 6, 0.30m, "NIFTY"));

    // ── Switches: every new gate is off by default and HeroZero is exempt ────────────────────

    public static IEnumerable<object[]> Gates() => new[]
    {
        new object[] { new CloseBreakoutGate(), CloseBreakoutGate.EnableKey },
        new object[] { new EfficiencyRatioGate(), EfficiencyRatioGate.EnableKey },
        new object[] { new StopNoiseGate(), StopNoiseGate.EnableKey },
    };

    [Theory]
    [MemberData(nameof(Gates))]
    public async Task NewGates_OffByDefault(IPostSignalGate gate, string _)
        => Assert.Null(await gate.BlockAsync(Context(new FakeStrategySettingsService()), Snapshot(Chop(10, 35m)), ChopSignal(), CancellationToken.None));

    [Theory]
    [MemberData(nameof(Gates))]
    public async Task NewGates_BlockChopWhenOn(IPostSignalGate gate, string key)
        => Assert.NotNull(await gate.BlockAsync(Context(new FakeStrategySettingsService().Set(key, 1)), Snapshot(Chop(10, 35m)), ChopSignal(), CancellationToken.None));

    [Theory]
    [MemberData(nameof(Gates))]
    public async Task NewGates_HeroZeroExempt(IPostSignalGate gate, string key)
    {
        var signal = ChopSignal();
        signal.StrategyType = TradingConst.StrategyType.HeroZero;
        Assert.Null(await gate.BlockAsync(Context(new FakeStrategySettingsService().Set(key, 1)), Snapshot(Chop(10, 35m)), signal, CancellationToken.None));
    }

    [Fact]
    public void NewGates_AreRegisteredOnceEach()
    {
        Assert.Single(SignalWorker.PostGates, g => g is CloseBreakoutGate);
        Assert.Single(SignalWorker.PostGates, g => g is EfficiencyRatioGate);
        Assert.Single(SignalWorker.PostGates, g => g is StopNoiseGate);
    }

    // ── CloseBreakoutGate side switch (CloseBreakoutGateSide) ─────────────

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(0, false, true)]
    [InlineData(1, true, true)]
    [InlineData(1, false, false)]
    [InlineData(2, true, false)]
    [InlineData(2, false, true)]
    public void CloseBreakoutGate_SideSwitch(int side, bool isCall, bool applies) =>
        Assert.Equal(applies, CloseBreakoutGate.AppliesTo(side, isCall));

    // ── Fix 4: MaxDailyTradesGate switch ─────────────────────────────────────────────────────

    [Fact]
    public async Task MaxDailyTradesGate_OffByDefault_NeverQueries()
        // SignalRepo is null here: a disabled gate must return before touching the repository.
        => Assert.Null(await new MaxDailyTradesGate().BlockAsync(Context(new FakeStrategySettingsService()), Snapshot(Chop(10, 35m)), CancellationToken.None));

    // ── Helpers (same shape as ChopGateTests) ────────────────────────────────────────────────

    /// <summary>CALL signal with a 20-point index stop, at the last chop close.</summary>
    private static Signal ChopSignal() => new()
    {
        Symbol = "NIFTY",
        SignalType = TradingConst.SignalType.CALL,
        StrategyType = TradingConst.StrategyType.Single,
        EntryPrice = 24035m,
        StopLoss = 24015m,
    };

    internal static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: "NIFTY",
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings(), EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);

    internal static MarketSnapshot Snapshot(List<Candle> candles5m) => new(
        Candles1m: new List<Candle>(),
        Candles5m: candles5m,
        OptionChain: new List<OptionChainData>(),
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Uptrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: 10m, Ltp: candles5m.Count > 0 ? candles5m[^1].Close : 0m,
        YesterdayClose: 0m, CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);
}
