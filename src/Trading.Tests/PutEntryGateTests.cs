using Trading.Domain;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Tests.Support;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The PUT entry specification — 11 conditions, the exact mirror of CALL.
///
///   1  EMA20 &lt; EMA50            7  Body% >= 30
///   2  LTP &lt; EMA20              8  Volume &gt; VolumeSMA20   (OFF - no index volume)
///   3  LTP &lt; VWAP  (OFF - no index volume)
///   4  LTP &lt; Prev 5m Low        9  ATR% >= 0.03
///   5  RSI 30-48                10  |PE Delta| 0.40-0.65
///   6  Bearish candle           11  DayChange% &lt; -0.15 OR (Fall% > 0.40 AND LTP &lt; DayOpen)
///
/// Fall% = (DayHigh - LTP) / DayHigh * 100.
///
/// DELTA SIGN is the one place the mirror is not literal: a put's delta is NEGATIVE (-0.50 for an
/// ATM PE), so the 0.40-0.65 band is applied to the absolute value. Comparing the raw negative
/// against a positive band would reject every put ever quoted — pinned by a test below.
/// </summary>
public class PutEntryGateTests
{
    private const decimal Spot = 20_000m;

    /// <summary>
    /// Every decimal threshold is explicitly Set at its own compiled-default value: since
    /// IsSettingActive now reads "never configured" as "does not apply" (matching the real
    /// StrategySettingsService), a condition that isn't Set at all would silently no-op instead
    /// of being enforced-and-satisfied, which is what this baseline fixture needs.
    /// </summary>
    private static FakeStrategySettingsService Settings() =>
        new FakeStrategySettingsService()
            .Set("EnableTrendFilter", 1)
            .Set("EnableRsiFilter", 1)
            .Set("EnableCandleFilter", 1)
            .Set("PutRsiFloor", 28m)
            .Set("PutRsiCeiling", 40m)
            .Set("PutOiChangeMinimum", -1000m)
            .Set("PutBodyPctMinimum", 30m)
            .Set("PutMinAtrPercent", 0.03m)
            .Set("PutDeltaMinimum", 0.35m)
            .Set("PutDeltaMaximum", 0.65m)
            .Set("PutDayChangePctMax", -0.10m)
            .Set("PutFallPctMin", 0.40m)
            .Set("PutPcrMaximum", 0.90m);

    /// <summary>Runs the real engine with all 11 PUT conditions satisfied.</summary>
    private static SignalGenerationResult RunPut(
        FakeStrategySettingsService? settings = null,
        decimal ema20 = Spot + 50m,
        decimal ema50 = Spot + 100m,
        decimal rsi = 40m,
        decimal pcr = 0.75m,          // <= 0.90 -> condition 12 passes
        decimal peChangeInOi = 0L,
        decimal prev5mLow = Spot + 10m,
        decimal vwap = 0m,
        decimal currentVolume = 0m,
        decimal avgVolume20 = 0m,
        decimal atrRaw = 30m,               // 0.15% >= 0.03
        decimal dayChangePct = -0.20m,      // < -0.10  -> day filter path A
        decimal dayOpen = Spot + 100m,
        decimal dayHigh = Spot + 100m,
        decimal candleOpen = Spot + 20m,    // body 20 of range 24 = 83%, bearish
        decimal candleClose = Spot,
        MarketState marketState = MarketState.Trend)
    {
        var engine = SignalEngineTestFactory.Create(settings ?? Settings());

        return engine.GenerateSignalDetailed(
            symbol: SignalEngineTestFactory.Symbol,
            ltp: Spot,
            ema20: ema20,
            ema50: ema50,
            pcr: pcr,
            rsi: rsi,
            currentVolume: currentVolume,
            avgVolume20: avgVolume20,
            priceChangePct: -0.50m,
            atrPct: 0.15m,
            atrRaw: atrRaw,
            optionChain: SignalEngineTestFactory.BuildChain(Spot, peChangeInOi: (long)peChangeInOi),   // PE Delta = -0.50
            strategyHint: TradingConst.StrategyType.Single,
            ema20_5m: Spot + 40m,
            ema50_5m: Spot + 90m,
            lastClosedCandle: SignalEngineTestFactory.MakeCandle(
                open: candleOpen, close: candleClose, high: Spot + 22m, low: Spot - 2m),
            marketState: marketState,
            vwap: vwap,
            prev5mLow: prev5mLow,
            dayOpen: dayOpen,
            dayHigh: dayHigh,
            dayChangePct: dayChangePct);
    }

    // ── Baseline ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllElevenSatisfied_PutFires()
    {
        var result = RunPut();

        Assert.NotNull(result.Signal);
        Assert.Equal(TradingConst.SignalType.PUT, result.Signal!.SignalType);
    }

    // ── 1 & 2: EMA structure ────────────────────────────────────────────────────────────────

    [Fact]
    public void Condition1_Ema20MustBeBelowEma50()
    {
        var r = RunPut(ema20: Spot + 100m, ema50: Spot + 50m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("1 EMA20 < EMA50"));
    }

    [Fact]
    public void Condition2_LtpMustBeBelowEma20()
    {
        var r = RunPut(ema20: Spot - 50m, ema50: Spot + 100m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("2 LTP < EMA20"));
    }

    // ── 3 & 8: dead inputs, off by default ──────────────────────────────────────────────────

    [Fact]
    public void Condition3_VwapIsCommentedOut_AndCanNeverBlock()
    {
        Assert.NotNull(RunPut(vwap: 0m).Signal);
        Assert.NotNull(RunPut(vwap: Spot - 10m).Signal);
        Assert.NotNull(RunPut(Settings().Set("EnablePutVwapFilter", 1), vwap: 0m).Signal);
    }

    [Fact]
    public void Condition8_VolumeIsCommentedOut_AndCanNeverBlock()
    {
        Assert.NotNull(RunPut(currentVolume: 0m, avgVolume20: 0m).Signal);
        Assert.NotNull(RunPut(currentVolume: 50m, avgVolume20: 100m).Signal);
        Assert.NotNull(RunPut(Settings().Set("EnablePutVolumeFilter", 1),
            currentVolume: 0m, avgVolume20: 0m).Signal);
    }

    // ── 4: breakdown under the last closed 5m low ───────────────────────────────────────────

    [Theory]
    [InlineData(20_010, true)]    // LTP below prior 5m low
    [InlineData(20_000, false)]   // equal is not below
    [InlineData(19_990, false)]   // above
    public void Condition4_Prev5mLowBreakdown(int prev5mLow, bool expected)
    {
        Assert.Equal(expected, RunPut(prev5mLow: prev5mLow).Signal != null);
    }

    [Fact]
    public void Condition4_MissingPrev5mLow_FailsClosed()
    {
        var r = RunPut(prev5mLow: 0m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("4 LTP < Prev5mLow"));
    }

    // ── 5: RSI band, tightened to 28-40 ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(27.99, false)]
    [InlineData(28.00, true)]     // the tightened floor
    [InlineData(35.00, true)]
    [InlineData(40.00, true)]     // the tightened ceiling
    [InlineData(40.01, false)]
    public void Condition5_RsiBand(double rsi, bool expected)
    {
        Assert.Equal(expected, RunPut(rsi: (decimal)rsi).Signal != null);
    }

    [Fact]
    public void Condition5_DeactivatingEitherBound_SkipsTheRsiCheckEntirely()
    {
        Assert.Null(RunPut(rsi: 95m).Signal);
        Assert.NotNull(RunPut(Settings().SetInactive("PutRsiCeiling"), rsi: 95m).Signal);
        Assert.NotNull(RunPut(Settings().SetInactive("PutRsiFloor"), rsi: 5m).Signal);
    }

    // ── 6 & 7: bearish candle, body >= 30% ──────────────────────────────────────────────────

    [Fact]
    public void Condition6_BullishCandleBlocks()
    {
        var r = RunPut(candleOpen: Spot, candleClose: Spot + 20m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("6+7 Bearish candle"));
    }

    [Fact]
    public void Condition7_ThinBodyBlocks_AndUsesThePutSpecificThreshold()
    {
        Assert.Null(RunPut(candleOpen: Spot + 2m, candleClose: Spot).Signal);

        Assert.NotNull(RunPut(Settings().Set("PutBodyPctMinimum", 5m),
            candleOpen: Spot + 2m, candleClose: Spot).Signal);
    }

    // ── 9: ATR floor ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Condition9_AtrFloorBlocksQuietMarkets()
    {
        var r = RunPut(Settings().Set("MinAtrPercent", 0.001m), atrRaw: 4m);   // 0.02%

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("9 ATR%"));
    }

    // ── 10: the delta sign trap ─────────────────────────────────────────────────────────────

    [Fact]
    public void Condition10_DeltaBandUsesAbsoluteValue_NotTheRawNegative()
    {
        // The chain's PE delta is -0.50. |-0.50| = 0.50 sits inside [0.35, 0.65], so the PUT must
        // fire. A naive `peAtmDelta >= 0.35` would reject every put ever quoted.
        Assert.NotNull(RunPut().Signal);
    }

    [Fact]
    public void Condition10_DeltaOutsideBandBlocks()
    {
        // Ceiling below 0.50.
        var r = RunPut(Settings().Set("PutDeltaMaximum", 0.45m));
        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("10 |PE Delta|"));

        // Floor above 0.50.
        Assert.Null(RunPut(Settings().Set("PutDeltaMinimum", 0.55m)).Signal);
    }

    // ── 11: day filter, both paths ──────────────────────────────────────────────────────────

    [Fact]
    public void Condition11_PathA_DayChangeAloneIsEnough()
    {
        // Fall path deliberately unsatisfied: LTP is at the day high.
        Assert.NotNull(RunPut(dayChangePct: -0.11m, dayHigh: Spot, dayOpen: Spot - 50m).Signal);
    }

    [Fact]
    public void Condition11_PathB_FallFromHighWithLtpBelowOpen()
    {
        // DayChange% fails, but price has fallen 0.50% off the high AND lost the open.
        // dayHigh 20,100 -> (20100-20000)/20100*100 = 0.498% > 0.40
        Assert.NotNull(RunPut(dayChangePct: 0.00m, dayHigh: 20_100m, dayOpen: Spot + 50m).Signal);
    }

    [Fact]
    public void Condition11_PathB_RequiresLtpBelowDayOpen()
    {
        var r = RunPut(dayChangePct: 0.00m, dayHigh: 20_100m, dayOpen: Spot - 50m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("11 DayChange%"));
    }

    [Fact]
    public void Condition11_NeitherPath_Blocks()
    {
        Assert.Null(RunPut(dayChangePct: -0.05m, dayHigh: Spot, dayOpen: Spot - 50m).Signal);
    }

    [Theory]
    [InlineData(-0.10, false)]   // strictly less than, so equal fails
    [InlineData(-0.11, true)]
    public void Condition11_DayChangeBoundary(double dayChangePct, bool expected)
    {
        Assert.Equal(expected,
            RunPut(dayChangePct: (decimal)dayChangePct, dayHigh: Spot, dayOpen: Spot - 50m).Signal != null);
    }

    // ── 12: PCR, off by default ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.66)]
    [InlineData(0.90)]
    [InlineData(1.20)]
    public void Condition12_PcrDoesNotBlock_WhenFilterDisabled(double pcr)
    {
        // Any PCR fires while EnablePutPcrFilter defaults to off. On the sample it was measured
        // against, PUT winners averaged 0.88 and losers 0.83 - fully overlapping, so it
        // discriminated nothing - hence the off-by-default posture. Note 1.20 would BLOCK under
        // the <= 0.90 gate if the filter were enabled.
        Assert.NotNull(RunPut(pcr: (decimal)pcr).Signal);
    }

    [Fact]
    public void Condition12_ThresholdAlone_IsInertWithoutTheFilterEnabled()
    {
        // Setting PutPcrMaximum does nothing on its own - EnablePutPcrFilter still defaults off.
        Assert.NotNull(RunPut(Settings().Set("PutPcrMaximum", 0.90m), pcr: 1.20m).Signal);
    }

    [Fact]
    public void Condition12_PcrGatesOnceExplicitlyEnabled()
    {
        var enabled = Settings().Set("EnablePutPcrFilter", 1);

        // Default PutPcrMaximum is 0.90 -> 1.20 now blocks, 0.66 still passes.
        var blocked = RunPut(enabled, pcr: 1.20m);
        Assert.Null(blocked.Signal);
        Assert.Contains(blocked.FailureReasons, x => x.Contains("12 PCR"));

        Assert.NotNull(RunPut(enabled, pcr: 0.66m).Signal);
    }

    // ── 13: OI change floor ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1000, true)]
    [InlineData(-1001, false)]
    public void Condition13_OiChangeFloor(int coi, bool expected)
    {
        Assert.Equal(expected, RunPut(peChangeInOi: coi).Signal != null);
    }

    // ── Failure reasons must describe only what is enforced ─────────────────────────────────

    [Fact]
    public void FailureReasons_NeverNameAnUnenforcedCondition()
    {
        var r = RunPut(rsi: 80m);

        Assert.Null(r.Signal);
        Assert.DoesNotContain(r.FailureReasons, x => x.Contains("StrongTrend"));
    }

    // ── CALL and PUT are structurally exclusive ─────────────────────────────────────────────

    [Fact]
    public void PutSetup_NeverProducesACall()
    {
        var result = RunPut();

        Assert.NotNull(result.Signal);
        Assert.NotEqual(TradingConst.SignalType.CALL, result.Signal!.SignalType);
    }
}
