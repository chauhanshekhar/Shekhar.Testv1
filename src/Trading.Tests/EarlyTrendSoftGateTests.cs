using Trading.Backtest.Data;
using Trading.Backtest.Indicators;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Tests.Fakes;
using Trading.SignalEngine.Services;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// EarlyTrendGate soft rule (EnableEarlyTrendSoftGate) and the 5m ATR that feeds it. A CALL the gate would block is allowed only on a gap day
/// (|gap| >= 0.30%), from 10:30 IST, with (EMA20_5m - EMA50_5m) / ATR5m strictly below 0.30. Missing or stale ATR must keep the block.
/// Design: docs/research/2026-11-11-early-trend-gapdays/ATR5M-DESIGN.md.
/// </summary>
public class EarlyTrendSoftGateTests
{
    private const string Call = TradingConst.SignalType.CALL, Put = TradingConst.SignalType.PUT;
    private static readonly TimeSpan T1030 = new(10, 30, 0);

    // ATR 10, EMA gap 2.9 => normalized 0.29. Gap +0.40%, 10:45, switch on: exempt unless a test changes one input.
    private static EarlyTrendGate.SoftGateDecision Soft(
        string signal = Call, decimal e20 = 102.9m, decimal e50 = 100m, decimal atr = 10m,
        decimal? gap = 0.40m, TimeSpan? tod = null, bool on = true)
        => EarlyTrendGate.SoftGateExempt(signal, e20, e50, atr, gap, tod ?? new TimeSpan(10, 45, 0), on);

    [Fact]
    public void AllConditions_Exempts() => Assert.True(Soft().Exempt);

    // ── boundaries ───────────────────────────────────────────────────────────────────────────────────
    [Fact] public void Exactly1030_IsExempt() => Assert.True(Soft(tod: T1030).Exempt);
    [Fact] public void OneSecondBefore1030_IsNotExempt() => Assert.False(Soft(tod: T1030 - TimeSpan.FromSeconds(1)).Exempt);

    [Fact] public void NormalizedGapExactly030_IsNotExempt() => Assert.False(Soft(e20: 103m).Exempt);                  // 3.0 / 10 = 0.30, strictly below required
    [Fact] public void NormalizedGapJustBelow030_IsExempt() => Assert.True(Soft(e20: 102.99m).Exempt);

    [Fact] public void GapExactly030PctUp_IsExempt() => Assert.True(Soft(gap: 0.30m).Exempt);
    [Fact] public void GapExactly030PctDown_IsExempt() => Assert.True(Soft(gap: -0.30m).Exempt);

    [Theory]
    [InlineData(0.2999)]
    [InlineData(-0.2999)]
    [InlineData(0)]
    public void GapBelow030Pct_NormalDay_IsNotExempt(double gap) => Assert.False(Soft(gap: (decimal)gap).Exempt);

    // ── negative / zero EMA gaps never trigger the exemption ────────────────────────────────────────
    [Theory]
    [InlineData(99.0, 100.0)]   // EMA20 below EMA50: negative gap (the gate does not block this case at all)
    [InlineData(100.0, 100.0)]  // flat
    public void NegativeOrZeroEmaGap_IsNotExempt(double e20, double e50)
    {
        var d = Soft(e20: (decimal)e20, e50: (decimal)e50);
        Assert.False(d.Exempt);
        Assert.True(d.NormalizedGap <= 0m);
    }

    // ── missing inputs keep the existing block ───────────────────────────────────────────────────────
    [Fact] public void ZeroAtr_IsNotExempt() { var d = Soft(atr: 0m); Assert.False(d.Exempt); Assert.Null(d.NormalizedGap); }
    [Fact] public void NegativeAtr_IsNotExempt() => Assert.False(Soft(atr: -5m).Exempt);
    [Fact] public void MissingGap_IsNotExempt() => Assert.False(Soft(gap: null).Exempt);
    [Fact] public void MissingEma_IsNotExempt() { Assert.False(Soft(e20: 0m).Exempt); Assert.False(Soft(e50: 0m).Exempt); }

    // ── scope ───────────────────────────────────────────────────────────────────────────────────────
    [Fact] public void SwitchOff_IsNotExempt() => Assert.False(Soft(on: false).Exempt);
    [Fact] public void Put_IsNeverExempt() => Assert.False(Soft(signal: Put).Exempt);

    [Fact]
    public void DefaultsAndExistingGate_Unchanged()
    {
        Assert.Equal("EnableEarlyTrendSoftGate", EarlyTrendGate.SoftGateKey);
        Assert.NotNull(EarlyTrendGate.Evaluate(Call, 55020m, 55000m, EarlyTrendGate.SideCallOnly));   // still blocks
        Assert.Null(EarlyTrendGate.Evaluate(Put, 54990m, 55000m, EarlyTrendGate.SideCallOnly));        // PUT still not gated by default
    }

    // ── Atr5m: missing / stale never allows a blocked CALL ───────────────────────────────────────────
    private static readonly DateTime Now = new(2026, 11, 11, 6, 0, 0, DateTimeKind.Utc);   // 11:30 IST

    /// <summary>n closed 5m candles, oldest first, the newest starting <paramref name="newestAge"/> before <see cref="Now"/>.</summary>
    private static List<Candle> Series(int n, TimeSpan newestAge)
    {
        var list = new List<Candle>();
        var rnd = new Random(7);
        decimal close = 55000m;
        for (var i = 0; i < n; i++)
        {
            var open = close;
            close = open + (decimal)(rnd.NextDouble() * 40 - 20);
            list.Add(new Candle
            {
                CandleTime = Now - newestAge - TimeSpan.FromMinutes(5 * (n - 1 - i)),
                Open = open, Close = close,
                High = Math.Max(open, close) + (decimal)(rnd.NextDouble() * 12),
                Low = Math.Min(open, close) - (decimal)(rnd.NextDouble() * 12),
            });
        }
        return list;
    }

    [Fact]
    public void Atr5mFor_FreshFullSeries_IsPositive()
        => Assert.True(MarketSnapshotBuilder.Atr5mFor(Series(100, TimeSpan.FromMinutes(5)), new AtrService(), Now) > 0m);

    [Fact]
    public void Atr5mFor_FewerThan15Bars_IsMissing()
    {
        Assert.Equal(0m, MarketSnapshotBuilder.Atr5mFor(Series(14, TimeSpan.FromMinutes(5)), new AtrService(), Now));
        Assert.True(MarketSnapshotBuilder.Atr5mFor(Series(15, TimeSpan.FromMinutes(5)), new AtrService(), Now) > 0m);
    }

    [Fact]
    public void Atr5mFor_StaleNewestBar_IsMissing_AndNeverExempts()
    {
        var stale = Series(100, TimeSpan.FromMinutes(16));          // newest bar started 16 min ago: feed stalled
        var atr = MarketSnapshotBuilder.Atr5mFor(stale, new AtrService(), Now);
        Assert.Equal(0m, atr);
        Assert.False(EarlyTrendGate.SoftGateExempt(Call, 55003m, 55000m, atr, 0.5m, new TimeSpan(11, 30, 0), true).Exempt);

        // the boundary: exactly 15 minutes old is still accepted, a second more is not
        Assert.True(MarketSnapshotBuilder.Atr5mFor(Series(100, TimeSpan.FromMinutes(15)), new AtrService(), Now) > 0m);
        Assert.Equal(0m, MarketSnapshotBuilder.Atr5mFor(Series(100, TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1)), new AtrService(), Now));
    }

    // ── parity: live AtrService vs the backtest's IndicatorMath.Atr on the same closed bars ─────────
    // Same candle ordering (oldest first), same TR definition, same initialisation (simple average of the first 14 TR, then Wilder), same 100-bar window.
    [Theory]
    [InlineData(15)]
    [InlineData(40)]
    [InlineData(100)]
    [InlineData(130)]   // longer history: both sides keep only the last 100 bars
    public void LiveAtr_MatchesBacktestAtr_OnTheSameBars(int bars)
    {
        var candles = Series(bars, TimeSpan.FromMinutes(5));
        var window = candles.TakeLast(100).ToList();                                             // MarketSnapshotBuilder: .TakeLast(100)
        var live = (double)MarketSnapshotBuilder.Atr5mFor(window, new AtrService(), Now);

        var all = candles.Select(c => new Bar(c.CandleTime, (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close)).ToArray();
        var backtest = IndicatorMath.Atr(all.AsSpan(Math.Max(0, all.Length - 100)));             // MarketSeriesBuilder: window of Window5m (100) closed bars

        Assert.Equal(backtest, live, 6);
    }

    [Fact]
    public void BacktestWindowConstant_Is100()
        => Assert.Equal(100, MarketSeriesBuilder.Window5m);

    [Fact]
    public void StaleThreshold_IsTenMinutesAfterTheBarClosed()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), MarketSnapshotBuilder.Atr5mMaxCloseAge);
        // newest bar STARTED 15 min ago = closed 10 min ago: accepted; one second older: missing
        Assert.True(MarketSnapshotBuilder.Atr5mFor(Series(100, TimeSpan.FromMinutes(15)), new AtrService(), Now) > 0m);
        Assert.Equal(0m, MarketSnapshotBuilder.Atr5mFor(Series(100, TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1)), new AtrService(), Now));
    }

    // ── the real gate, with the clock passed in (BlockAt) ────────────────────────────────────────────
    private static readonly DateTime Day = new(2026, 11, 12);

    private static Task<string?> Gate(FakeStrategySettingsService settings, TimeSpan istTime, decimal atr5m = 20m, decimal dayOpen = 55220m,
        string signalType = Call)
        => new EarlyTrendGate().BlockAt(
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
                Ema20: 0m, Ema50: 0m, Ema20_5m: 55003m, Ema50_5m: 55000m,           // EMA gap 3 => normalized 3 / 20 = 0.15 with ATR 20
                Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: 48m, Ltp: 55000m, YesterdayClose: 55000m,
                CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
                DayOpen: dayOpen, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
                StrategyHint: "", NewsEvent: null,
                LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null) { Atr5m = atr5m },
            new Signal { Symbol = "BANKNIFTY", SignalType = signalType, StrategyType = TradingConst.StrategyType.Single },
            Day + istTime, CancellationToken.None);

    private static FakeStrategySettingsService GateOn() => new FakeStrategySettingsService().Set(EarlyTrendGate.EnableKey, 1);
    private static FakeStrategySettingsService SoftOn() => GateOn().Set(EarlyTrendGate.SoftGateKey, 1);

    [Fact]
    public async Task Gate_At1030_AllowsTheCall_AndAtOneSecondBefore_StillBlocks()
    {
        Assert.Null(await Gate(SoftOn(), new TimeSpan(10, 30, 0)));
        var before = await Gate(SoftOn(), new TimeSpan(10, 29, 59));
        Assert.NotNull(before);
        Assert.Contains("5m trend already up", before);
    }

    [Fact]
    public async Task Gate_SoftSwitchOffByDefault_StillBlocks_OnAGapDayAfter1030()
        => Assert.NotNull(await Gate(GateOn(), new TimeSpan(11, 0, 0)));

    [Fact]
    public async Task Gate_MissingOrStaleAtr_KeepsTheBlock()
        => Assert.NotNull(await Gate(SoftOn(), new TimeSpan(11, 0, 0), atr5m: 0m));

    [Fact]
    public async Task Gate_NormalDay_KeepsTheBlock()
        => Assert.NotNull(await Gate(SoftOn(), new TimeSpan(11, 0, 0), dayOpen: 55050m));          // gap +0.09%

    [Fact]
    public async Task Gate_GapExactly030Pct_Allows_And029Blocks()
    {
        Assert.Null(await Gate(SoftOn(), new TimeSpan(11, 0, 0), dayOpen: 55165m));               // +0.30% exactly
        Assert.NotNull(await Gate(SoftOn(), new TimeSpan(11, 0, 0), dayOpen: 55159.5m));          // +0.29909%... below the threshold
    }

    [Fact]
    public async Task Gate_GapDown_Allows()
        => Assert.Null(await Gate(SoftOn(), new TimeSpan(11, 0, 0), dayOpen: 54835m));            // -0.30% exactly

    [Fact]
    public async Task Gate_ExistingGapUpMorningException_StillWorksBeforeTheSoftRuleStarts()
        => Assert.Null(await Gate(GateOn().Set(EarlyTrendGate.GapUpMorningKey, 1), new TimeSpan(9, 45, 0)));   // soft rule off, 09:45 exception on

    [Fact]
    public async Task Gate_GateOff_NeverBlocks()
        => Assert.Null(await Gate(new FakeStrategySettingsService(), new TimeSpan(11, 0, 0), atr5m: 0m));
}
