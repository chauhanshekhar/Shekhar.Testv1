using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Tests.Fakes;
using Trading.Tests.Support;
using Trading.Worker;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Target2RMultiple (reward geometry), Trend5mGate (5m EMA20/EMA50 direction), OpeningWarmupGate (no entries
/// before the 1m EMAs reflect today), plus the riskAtr override end to end. All off / unchanged by default.
///
/// Engine numbers below use the default TradingSettings: AtrStopLossMultiplier 1.5, AtrTarget1Multiplier 1.5,
/// AtrTarget2Multiplier 1.95, stop clamp [20, 100]. atrRaw 30 -> stop 45 pts, T1 +45 (1R), T2 +58.5 (1.3R).
/// Chain: premium 100, delta 0.5.
/// </summary>
public class TargetAndTrendFixTests
{
    private const decimal Spot = 20_000m;

    private static Signal Build(FakeStrategySettingsService settings, bool call = true, decimal riskAtr = 0m)
    {
        var engine = SignalEngineTestFactory.Create(settings);
        var result = engine.GenerateRuleSignal(
            symbol: SignalEngineTestFactory.Symbol,
            optionType: call ? TradingConst.OptionType.CE : TradingConst.OptionType.PE,
            call: call,
            ltp: Spot, ema20: Spot, ema50: Spot, pcr: 1.00m, rsi: 60m,
            currentVolume: 0m, avgVolume20: 0m, atrRaw: 30m,
            optionChain: SignalEngineTestFactory.BuildChain(Spot),
            lastClosedCandle: null,
            out _,
            riskAtr);

        Assert.NotNull(result.Signal);
        return result.Signal!;
    }

    // ── Target2RMultiple ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Target2_NoRow_KeepsDefault1_3R()
    {
        var s = Build(new FakeStrategySettingsService());
        Assert.Equal(Spot + 45m, s.Target1);
        Assert.Equal(Spot + 58.5m, s.Target2);
        Assert.Equal(129.25m, s.OptionT2);          // 100 + 45 x 1.3 x 0.5
    }

    [Fact]
    public void Target2_RowAt2R_MovesIndexAndOptionT2_T1Unchanged()
    {
        var s = Build(new FakeStrategySettingsService().Set("Target2RMultiple", 2m));
        Assert.Equal(Spot + 45m, s.Target1);
        Assert.Equal(Spot + 90m, s.Target2);
        Assert.Equal(145m, s.OptionT2);             // 100 + 45 x 2 x 0.5
    }

    [Fact]
    public void Target2_Put_IsBelowEntry()
        => Assert.Equal(Spot - 90m, Build(new FakeStrategySettingsService().Set("Target2RMultiple", 2m), call: false).Target2);

    [Fact]
    public void Target2_NotAboveT1_IsIgnored()
        => Assert.Equal(Spot + 58.5m, Build(new FakeStrategySettingsService().Set("Target2RMultiple", 1m)).Target2);

    [Fact]
    public void Target2_InactiveRow_IsIgnored()
        => Assert.Equal(Spot + 58.5m,
            Build(new FakeStrategySettingsService().Set("Target2RMultiple", 2m).SetInactive("Target2RMultiple")).Target2);

    // ── riskAtr override (EnableAtr5mStop path) ──────────────────────────────────────────────

    [Fact]
    public void RiskAtr_ReplacesAtrForStopOnly()
    {
        var s = Build(new FakeStrategySettingsService(), riskAtr: 40m);   // 40 x 1.5 = 60-pt stop
        Assert.Equal(Spot - 60m, s.StopLoss);
        Assert.Equal(Spot + 60m, s.Target1);
    }

    // ── Trend5mGate ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, 24100, 24000, false)]   // CALL, 5m EMA20 above EMA50: pass
    [InlineData(true, 23900, 24000, true)]    // CALL against the 5m trend: block
    [InlineData(false, 23900, 24000, false)]  // PUT with the trend: pass
    [InlineData(false, 24100, 24000, true)]   // PUT against: block
    [InlineData(true, 0, 24000, true)]        // missing EMA: fail closed
    public void Trend5m_Direction(bool call, int ema20, int ema50, bool blocks)
        => Assert.Equal(blocks, Trend5mGate.Evaluate(call, ema20, ema50, 24050m, requirePriceSide: false) is not null);

    [Fact]
    public void Trend5m_PriceSide_RequiresLtpBeyondEma20()
    {
        Assert.NotNull(Trend5mGate.Evaluate(true, 24100m, 24000m, ltp: 24050m, requirePriceSide: true));
        Assert.Null(Trend5mGate.Evaluate(true, 24100m, 24000m, ltp: 24150m, requirePriceSide: true));
    }

    // ── OpeningWarmupGate ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(9 * 60 + 15, true)]
    [InlineData(9 * 60 + 44, true)]
    [InlineData(9 * 60 + 45, false)]   // end is exclusive
    [InlineData(13 * 60, false)]
    [InlineData(9 * 60, false)]        // before the open: not this gate's job
    public void Warmup_BlocksUntilEnd(int minute, bool blocks)
        => Assert.Equal(blocks, OpeningWarmupGate.Evaluate(minute, OpeningWarmupGate.DefaultEndMinute) is not null);

    // ── Switches and registration ────────────────────────────────────────────────────────────

    [Fact]
    public async Task NewGates_OffByDefault()
    {
        var signal = new Signal { Symbol = "NIFTY", SignalType = TradingConst.SignalType.CALL, StrategyType = TradingConst.StrategyType.Single };
        var ctx = ChoppyMarketFixTests.Context(new FakeStrategySettingsService());
        var market = ChoppyMarketFixTests.Snapshot(new List<Candle>());

        Assert.Null(await new Trend5mGate().BlockAsync(ctx, market, signal, CancellationToken.None));
        Assert.Null(await new OpeningWarmupGate().BlockAsync(ctx, market, signal, CancellationToken.None));
    }

    [Fact]
    public void NewGates_AreRegisteredOnce_AndNoTradeWindowOrderKept()
    {
        Assert.Single(SignalWorker.PostGates, g => g is Trend5mGate);
        Assert.Single(SignalWorker.PostGates, g => g is OpeningWarmupGate);

        var early = Array.FindIndex(SignalWorker.PostGates, g => g is EarlyTrendGate);
        var window = Array.FindIndex(SignalWorker.PostGates, g => g is NoTradeWindowGate);
        Assert.Equal(early + 1, window);
    }
}
