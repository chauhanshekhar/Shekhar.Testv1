using Trading.Domain;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Tests.Support;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The CALL entry specification — 11 conditions, replacing the previous CALL chain.
///
///   1  EMA20 &gt; EMA50            7  Body% >= 30
///   2  LTP &gt; EMA20              8  Volume &gt; VolumeSMA20   (OFF - no index volume)
///   3  LTP &gt; VWAP  (OFF - no index volume)
///   4  LTP &gt; Prev 5m High       9  ATR% >= 0.03
///   5  RSI 52-70                10  CE Delta 0.40-0.65
///   6  Bullish candle           11  DayChange% > 0.15 OR (Recovery% > 0.40 AND LTP > DayOpen)
///
/// Conditions 3 and 8 are flag-gated OFF because their inputs do not exist: Groww publishes no
/// index candle volume, so VWAP is NULL on every row and every Candle.Volume is 0. Wiring them
/// live would suppress every CALL forever. The tests below pin BOTH halves of that: inert while
/// disabled, and fail-closed once enabled without data.
///
/// PUT and HeroZero are untouched; the regressions at the bottom hold that line.
/// </summary>
public class CallEntryGateTests
{
    private const decimal Spot = 20_000m;

    /// <summary>
    /// Every non-CALL-specific filter passes, so one mutation isolates one condition. Every
    /// decimal threshold is also explicitly Set here at its own compiled-default value: since
    /// IsSettingActive now reads "never configured" as "does not apply" (matching the real
    /// StrategySettingsService), a condition that isn't Set at all would silently no-op instead
    /// of being enforced-and-satisfied, which is what "every condition passes" is supposed to
    /// mean for a baseline fixture.
    /// </summary>
    private static FakeStrategySettingsService Settings() =>
        new FakeStrategySettingsService()
            .Set("EnableTrendFilter", 1)
            .Set("EnableRsiFilter", 1)
            .Set("EnableCandleFilter", 1)
            .Set("CallRsiFloor", 60m)
            .Set("CallRsiCeiling", 70m)
            .Set("CallOiChangeMinimum", -1000m)
            .Set("CallBodyPctMinimum", 30m)
            .Set("CallMinAtrPercent", 0.03m)
            .Set("CallDeltaMinimum", 0.35m)
            .Set("CallDeltaMaximum", 0.65m)
            .Set("CallDayChangePctMin", 0.10m)
            .Set("CallRecoveryPctMin", 0.40m)
            .Set("CallPcrMinimum", 1.05m);

    /// <summary>
    /// Runs the real engine with all 11 conditions satisfied. Each parameter is overridable so a
    /// test can break exactly one of them.
    /// </summary>
    private static SignalGenerationResult RunCall(
        FakeStrategySettingsService? settings = null,
        decimal ema20 = Spot - 50m,
        decimal ema50 = Spot - 100m,
        decimal rsi = 60m,
        decimal pcr = 1.20m,          // >= 1.05 -> condition 12 passes
        decimal ceChangeInOi = 0L,
        decimal prev5mHigh = Spot - 10m,
        decimal vwap = 0m,
        decimal currentVolume = 0m,
        decimal avgVolume20 = 0m,
        decimal atrRaw = 30m,               // 30/20000*100 = 0.15% >= 0.03
        decimal dayChangePct = 0.20m,       // > 0.10  -> day filter path A
        decimal dayOpen = Spot - 100m,
        decimal dayLow = Spot - 100m,
        decimal candleOpen = Spot - 20m,    // body 20 of range 24 = 83% >= 30, bullish
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
            priceChangePct: 0.50m,
            atrPct: 0.15m,
            atrRaw: atrRaw,
            optionChain: SignalEngineTestFactory.BuildChain(Spot, ceChangeInOi: (long)ceChangeInOi),   // CE Delta = 0.50
            strategyHint: TradingConst.StrategyType.Single,
            ema20_5m: Spot - 40m,
            ema50_5m: Spot - 90m,
            lastClosedCandle: SignalEngineTestFactory.MakeCandle(
                open: candleOpen, close: candleClose, high: Spot + 2m, low: Spot - 22m),
            marketState: marketState,
            vwap: vwap,
            prev5mHigh: prev5mHigh,
            dayOpen: dayOpen,
            dayLow: dayLow,
            dayChangePct: dayChangePct);
    }

    // ── Baseline ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllElevenSatisfied_CallFires()
    {
        var result = RunCall();

        Assert.NotNull(result.Signal);
        Assert.Equal(TradingConst.SignalType.CALL, result.Signal!.SignalType);
    }

    [Fact]
    public void MarketStateNoLongerGatesCall()
    {
        // The retired StrongTrend gate: CALL must now fire in any state, including the ones the
        // old specification rejected.
        foreach (var state in new[]
                 { MarketState.Trend, MarketState.Scalping, MarketState.Range, MarketState.Breakout })
        {
            Assert.NotNull(RunCall(marketState: state).Signal);
        }
    }

    // ── 1 & 2: EMA structure ────────────────────────────────────────────────────────────────

    [Fact]
    public void Condition1_Ema20MustExceedEma50()
    {
        var r = RunCall(ema20: Spot - 100m, ema50: Spot - 50m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("1 EMA20 > EMA50"));
    }

    [Fact]
    public void Condition2_LtpMustExceedEma20()
    {
        var r = RunCall(ema20: Spot + 50m, ema50: Spot - 100m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("2 LTP > EMA20"));
    }

    // ── 3 & 8: the two conditions whose inputs do not exist ─────────────────────────────────

    [Fact]
    public void Condition3_VwapIsCommentedOut_AndCanNeverBlock()
    {
        // VWAP is derived from volume, which indices do not publish, so Vwap is NULL on every
        // cycle. The condition is removed from the chain, so no VWAP value can block.
        Assert.NotNull(RunCall(vwap: 0m).Signal);

        // Even a VWAP that would FAIL the old condition (LTP below it) does not block.
        Assert.NotNull(RunCall(vwap: Spot + 10m).Signal);

        // And the setting is inert - it is no longer read.
        Assert.NotNull(RunCall(Settings().Set("EnableCallVwapFilter", 1), vwap: 0m).Signal);
    }

    [Fact]
    public void Condition8_VolumeIsCommentedOut_AndCanNeverBlock()
    {
        // Condition 8 is removed from the chain entirely, not just disabled: index candles carry
        // Volume=0, so there is no data for it to read. Zero volume must not block, and no
        // failure reason may mention volume.
        var r = RunCall(currentVolume: 0m, avgVolume20: 0m);
        Assert.NotNull(r.Signal);

        // Even with volume that would FAIL the old condition, nothing blocks.
        Assert.NotNull(RunCall(currentVolume: 50m, avgVolume20: 100m).Signal);

        // And the setting is inert - it is no longer read at all.
        Assert.NotNull(RunCall(Settings().Set("EnableCallVolumeFilter", 1),
            currentVolume: 0m, avgVolume20: 0m).Signal);
    }

    // ── 4: breakout over the last closed 5m high ────────────────────────────────────────────

    [Theory]
    [InlineData(19_990, true)]    // LTP above prior 5m high
    [InlineData(20_000, false)]   // equal is not above
    [InlineData(20_010, false)]   // below
    public void Condition4_Prev5mHighBreakout(int prev5mHigh, bool expected)
    {
        Assert.Equal(expected, RunCall(prev5mHigh: prev5mHigh).Signal != null);
    }

    [Fact]
    public void Condition4_MissingPrev5mHigh_FailsClosed()
    {
        // No closed 5m bar yet -> unknown, which must never read as confirmed.
        var r = RunCall(prev5mHigh: 0m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("4 LTP > Prev5mHigh"));
    }

    // ── 5: RSI band, tightened to 60-70 ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(59.99, false)]
    [InlineData(60.00, true)]     // the tightened floor
    [InlineData(65.00, true)]
    [InlineData(70.00, true)]
    [InlineData(70.01, false)]
    public void Condition5_RsiBand(double rsi, bool expected)
    {
        Assert.Equal(expected, RunCall(rsi: (decimal)rsi).Signal != null);
    }

    [Fact]
    public void Condition5_DeactivatingEitherBound_SkipsTheRsiCheckEntirely()
    {
        // RSI 95 is far outside the 60-70 band and normally blocks.
        Assert.Null(RunCall(rsi: 95m).Signal);

        // Deactivate the CEILING row -> the whole RSI condition is skipped, not just the upper
        // bound. IsActive = 0 means "do not verify this", independently of SettingValue.
        Assert.NotNull(RunCall(Settings().SetInactive("CallRsiCeiling"), rsi: 95m).Signal);

        // The FLOOR row does the same, and an RSI below the band then also passes.
        Assert.NotNull(RunCall(Settings().SetInactive("CallRsiFloor"), rsi: 5m).Signal);
    }

    [Fact]
    public void Condition5_DeactivatedCeiling_NeverCollapsesToZero()
    {
        // The hazard avoided: if an inactive ceiling resolved to 0, the check would read
        // "RSI <= 0" and NO CALL could pass. A normal in-band RSI must still fire.
        Assert.NotNull(RunCall(Settings().SetInactive("CallRsiCeiling"), rsi: 65m).Signal);
    }

    // ── 6 & 7: bullish candle with a body of at least 30% ───────────────────────────────────

    [Fact]
    public void Condition6_BearishCandleBlocks()
    {
        var r = RunCall(candleOpen: Spot, candleClose: Spot - 20m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("6+7 Bullish candle"));
    }

    [Fact]
    public void Condition7_ThinBodyBlocks_AndUsesTheCallSpecificThreshold()
    {
        // Body 2 of range 24 = 8.3%, below 30.
        Assert.Null(RunCall(candleOpen: Spot - 2m, candleClose: Spot).Signal);

        // PUT's shared BodyPctMinimum must not be what CALL reads.
        Assert.NotNull(RunCall(Settings().Set("CallBodyPctMinimum", 5m),
            candleOpen: Spot - 2m, candleClose: Spot).Signal);
    }

    // ── 9: ATR floor ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Condition9_AtrFloorBlocksQuietMarkets()
    {
        // atrRaw 4 on a 20,000 spot = 0.02%, below the 0.03% CALL floor.
        var r = RunCall(Settings().Set("MinAtrPercent", 0.001m), atrRaw: 4m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("9 ATR%"));
    }

    // ── 10: delta band, now with an upper bound ─────────────────────────────────────────────

    [Fact]
    public void Condition10_DeltaBandHasAnUpperBound()
    {
        // The chain's CE delta is 0.50 - inside [0.35, 0.65] by default.
        Assert.NotNull(RunCall().Signal);

        // The chain's CE delta is 0.50 - inside [0.35, 0.65]. Narrow the ceiling below it.
        var r = RunCall(Settings().Set("CallDeltaMaximum", 0.45m));

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("10 CE Delta"));
    }

    [Fact]
    public void Condition10_DeltaBelowFloorBlocks()
    {
        Assert.Null(RunCall(Settings().Set("CallDeltaMinimum", 0.55m)).Signal);
    }

    // ── 11: the day filter, both paths ──────────────────────────────────────────────────────

    [Fact]
    public void Condition11_PathA_DayChangeAloneIsEnough()
    {
        // Recovery path deliberately unsatisfied: LTP is at the day low.
        Assert.NotNull(RunCall(dayChangePct: 0.11m, dayLow: Spot, dayOpen: Spot + 50m).Signal);
    }

    [Fact]
    public void Condition11_PathB_RecoveryFromLowWithLtpAboveOpen()
    {
        // DayChange% fails, but price has recovered 0.50% off the low AND reclaimed the open.
        // dayLow 19,900 -> (20000-19900)/19900*100 = 0.503% > 0.40
        Assert.NotNull(RunCall(dayChangePct: 0.00m, dayLow: 19_900m, dayOpen: Spot - 50m).Signal);
    }

    [Fact]
    public void Condition11_PathB_RequiresLtpAboveDayOpen()
    {
        // Same recovery, but price has NOT reclaimed the open -> no trade.
        var r = RunCall(dayChangePct: 0.00m, dayLow: 19_900m, dayOpen: Spot + 50m);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("11 DayChange%"));
    }

    [Fact]
    public void Condition11_NeitherPath_Blocks()
    {
        // Flat day, no recovery.
        Assert.Null(RunCall(dayChangePct: 0.05m, dayLow: Spot, dayOpen: Spot + 50m).Signal);
    }

    [Theory]
    [InlineData(0.10, false)]   // strictly greater than, so equal fails
    [InlineData(0.11, true)]
    public void Condition11_DayChangeBoundary(double dayChangePct, bool expected)
    {
        Assert.Equal(expected,
            RunCall(dayChangePct: (decimal)dayChangePct, dayLow: Spot, dayOpen: Spot + 50m).Signal != null);
    }

    // ── 12: PCR, off by default ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.60)]
    [InlineData(0.90)]
    [InlineData(1.05)]
    [InlineData(1.40)]
    public void Condition12_PcrDoesNotBlock_WhenFilterDisabled(double pcr)
    {
        // Any PCR fires while EnableCallPcrFilter defaults to off. The gate was counter-indicated
        // on the sample it was measured against: CALL winners averaged 0.90, losers 1.17, so
        // ">= 1.05" kept the losers and dropped the winners - hence the off-by-default posture.
        Assert.NotNull(RunCall(pcr: (decimal)pcr).Signal);
    }

    [Fact]
    public void Condition12_ThresholdAlone_IsInertWithoutTheFilterEnabled()
    {
        // Setting CallPcrMinimum does nothing on its own - EnableCallPcrFilter still defaults off.
        Assert.NotNull(RunCall(Settings().Set("CallPcrMinimum", 1.05m), pcr: 0.60m).Signal);
    }

    [Fact]
    public void Condition12_PcrGatesOnceExplicitlyEnabled()
    {
        var enabled = Settings().Set("EnableCallPcrFilter", 1);

        // Default CallPcrMinimum is 1.05 -> 0.60 now blocks, 1.10 still passes.
        var blocked = RunCall(enabled, pcr: 0.60m);
        Assert.Null(blocked.Signal);
        Assert.Contains(blocked.FailureReasons, x => x.Contains("12 PCR"));

        Assert.NotNull(RunCall(enabled, pcr: 1.10m).Signal);
    }

    // ── 13: OI change floor ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, true)]        // 43.5% of live rows are exactly 0 - must NOT be rejected
    [InlineData(-1000, true)]    // the tolerance boundary, inclusive
    [InlineData(-1001, false)]
    [InlineData(50000, true)]
    public void Condition13_OiChangeFloor(int coi, bool expected)
    {
        Assert.Equal(expected, RunCall(ceChangeInOi: coi).Signal != null);
    }

    [Fact]
    public void Condition13_StrictPositiveVariantIsAvailable()
    {
        // Setting the floor to 1 gives the strict "> 0" reading from the specification.
        var s = Settings().Set("CallOiChangeMinimum", 1m);

        Assert.Null(RunCall(s, ceChangeInOi: 0).Signal);
        Assert.NotNull(RunCall(s, ceChangeInOi: 1).Signal);
    }

    // ── Failure reasons must describe only what is actually enforced ────────────────────────

    [Fact]
    public void FailureReasons_NeverNameAnUnenforcedCondition()
    {
        // A previous version reported "MarketState is StrongTrend" while that condition was
        // commented out of the `call` expression, so a live diagnosis blamed the wrong gate.
        var r = RunCall(rsi: 20m);

        Assert.Null(r.Signal);
        Assert.DoesNotContain(r.FailureReasons, x => x.Contains("StrongTrend"));
        Assert.DoesNotContain(r.FailureReasons, x => x.Contains("PCR >="));
    }

    // PUT coverage lives in PutEntryGateTests — it now has its own 11-condition
    // specification and no longer shares thresholds or inputs with CALL.
}
