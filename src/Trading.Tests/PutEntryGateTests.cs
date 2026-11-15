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
        MarketState marketState = MarketState.Trend,
        decimal? rsi5m = null,
        List<Trading.Domain.Entities.OptionChainData>? optionChain = null)
    {
        var engine = SignalEngineTestFactory.Create(settings ?? Settings());

        return engine.GenerateSignalDetailed(
            symbol: SignalEngineTestFactory.Symbol,
            ltp: Spot,
            ema20: ema20,
            ema50: ema50,
            pcr: pcr,
            rsi: rsi,
            rsi5m: rsi5m ?? rsi,   // entry RSI is 5m; default mirrors the 1m value
            currentVolume: currentVolume,
            avgVolume20: avgVolume20,
            priceChangePct: -0.50m,
            atrPct: 0.15m,
            atrRaw: atrRaw,
            optionChain: optionChain ?? SignalEngineTestFactory.BuildChain(Spot, peChangeInOi: (long)peChangeInOi),   // PE Delta = -0.50
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

    [Fact]
    public void Condition5_UsesFiveMinuteRsi_NotOneMinute()
    {
        var blocked = RunPut(rsi: 35m, rsi5m: 60m);
        Assert.Null(blocked.Signal);
        Assert.Contains(blocked.FailureReasons, x => x.Contains("5m RSI was 60.00"));

        Assert.NotNull(RunPut(rsi: 80m, rsi5m: 35m).Signal);
    }

    [Fact]
    public void Condition5_NullFiveMinuteRsi_FailsClosed()
    {
        var engine = SignalEngineTestFactory.Create(Settings());
        var r = engine.GenerateSignalDetailed(
            symbol: SignalEngineTestFactory.Symbol, ltp: Spot, ema20: Spot + 50m, ema50: Spot + 100m,
            pcr: 0.75m, rsi: 35m, currentVolume: 0m, avgVolume20: 0m, priceChangePct: -0.50m,
            atrPct: 0.15m, atrRaw: 30m,
            optionChain: SignalEngineTestFactory.BuildChain(Spot),
            strategyHint: TradingConst.StrategyType.Single,
            lastClosedCandle: SignalEngineTestFactory.MakeCandle(
                open: Spot + 20m, close: Spot, high: Spot + 22m, low: Spot - 2m),
            prev5mLow: Spot + 10m, dayOpen: Spot + 100m, dayHigh: Spot + 100m, dayChangePct: -0.20m,
            rsi5m: null);

        Assert.Null(r.Signal);
        Assert.Contains(r.FailureReasons, x => x.Contains("5m RSI was N/A"));
    }

    // ── 5b: optional second RSI band (EnablePutRsiBand2) ───────────────────────────────────

    private static FakeStrategySettingsService Band2(bool enabled = true) =>
        Settings()
            .Set("EnablePutRsiBand2", enabled ? 1 : 0)
            .Set("PutRsiBand2Floor", 42m)
            .Set("PutRsiBand2Ceiling", 49m);

    [Fact]
    public void Band2_OffByDefault_RsiAboveCeilingStillRejected()
        => Assert.Null(RunPut(Settings(), rsi: 45m).Signal);

    [Fact]
    public void Band2_On_AcceptsRsiInsideSecondBand()
        => Assert.NotNull(RunPut(Band2(), rsi: 45m).Signal);

    [Fact]
    public void Band2_On_FirstBandStillAccepted()
        => Assert.NotNull(RunPut(Band2(), rsi: 35m).Signal);

    [Theory]
    [InlineData(41.0)]   // gap between band 1 (<=40) and band 2 (>=42) stays rejected
    [InlineData(49.5)]   // above band 2
    [InlineData(27.9)]   // below band 1
    public void Band2_On_OutsideBothBands_Rejected(double rsi)
        => Assert.Null(RunPut(Band2(), rsi: (decimal)rsi).Signal);

    [Fact]
    public void Band2_SwitchedOff_BehavesLikeNoBand2()
        => Assert.Null(RunPut(Band2(enabled: false), rsi: 45m).Signal);

    // ── 2b: EMA gap (shared: EnableEmaGapFilter, EmaGapMinPoints, EmaGapMaxPoints) ─────────

    private static FakeStrategySettingsService Gap(bool enabled = true, decimal min = 10m) =>
        Settings().Set("EnableEmaGapFilter", enabled ? 1 : 0).Set("EmaGapMinPoints", min);

    [Fact]
    public void EmaGap_OffByDefault_TinyGapStillAccepted()
        => Assert.NotNull(RunPut(Settings(), ema20: Spot + 50m, ema50: Spot + 55m).Signal);   // gap 5

    [Theory]
    [InlineData(50, 100, true)]     // gap 50
    [InlineData(50, 61, true)]      // gap 11
    [InlineData(50, 60, false)]     // gap exactly 10 -> strict >
    [InlineData(50, 55, false)]     // gap 5
    public void EmaGap_On_RequiresGapAboveMinimum(int ema20Offset, int ema50Offset, bool accepted)
    {
        var r = RunPut(Gap(), ema20: Spot + ema20Offset, ema50: Spot + ema50Offset);
        Assert.Equal(accepted, r.Signal != null);
    }

    [Fact]
    public void EmaGap_On_MinimumIsConfigurable()
        => Assert.NotNull(RunPut(Gap(min: 4m), ema20: Spot + 50m, ema50: Spot + 55m).Signal);   // gap 5 > 4

    [Fact]
    public void EmaGap_SwitchedOff_BehavesLikeNoFilter()
        => Assert.NotNull(RunPut(Gap(enabled: false), ema20: Spot + 50m, ema50: Spot + 55m).Signal);

    [Fact]
    public void EmaGap_MaxBound_RejectsGapAtOrAboveMaximum()
    {
        var s = Gap().Set("EmaGapMaxPoints", 40m);
        Assert.NotNull(RunPut(s, ema20: Spot + 50m, ema50: Spot + 80m).Signal);   // gap 30
        Assert.Null(RunPut(s, ema20: Spot + 50m, ema50: Spot + 90m).Signal);      // gap 40 -> strict <
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

    // ── 12b: PCR floor, off unless PutPcrMinimum has a row ──────────────────────────────────
    // (No row + filter enabled is covered above: Condition12_PcrGatesOnceExplicitlyEnabled fires at 0.66.)

    [Fact]
    public void Condition12b_FloorAlone_IsInertWithoutTheFilterEnabled()
    {
        // PutPcrMinimum does nothing on its own - EnablePutPcrFilter still defaults off.
        Assert.NotNull(RunPut(Settings().Set("PutPcrMinimum", 0.70m), pcr: 0.60m).Signal);
    }

    [Fact]
    public void Condition12b_FloorGatesOnceEnabled()
    {
        var enabled = Settings().Set("EnablePutPcrFilter", 1).Set("PutPcrMinimum", 0.70m);

        // SENSEX PUT at PCR < 0.70 lost in the 2026-09-30 backtest -> 0.60 blocks, 0.75 passes
        // (and stays under the 0.90 ceiling of condition 12).
        var blocked = RunPut(enabled, pcr: 0.60m);
        Assert.Null(blocked.Signal);
        Assert.Contains(blocked.FailureReasons, x => x.Contains("12b PCR"));

        Assert.NotNull(RunPut(enabled, pcr: 0.75m).Signal);
    }

    [Fact]
    public void Condition12b_BoundaryIsInclusive()
        => Assert.NotNull(RunPut(Settings().Set("EnablePutPcrFilter", 1).Set("PutPcrMinimum", 0.70m), pcr: 0.70m).Signal);

    // ── Traded strike ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void IlliquidAtmStrike_StoresTheTradedNeighboursStrike()
    {
        // 2026-10-05 signal 124: ATM 54400, but the liquid leg (and the one traded) was 54500PE -
        // StrikePrice stored 54400. Here the ATM PE fails NIFTY's volume floor (200,000), so the
        // neighbouring 20050 PE is the liquid leg; StrikePrice must say 20050, matching the symbol.
        var chain = SignalEngineTestFactory.BuildChain(Spot);
        chain.Single(o => o.OptionType == TradingConst.OptionType.PE).Volume = 1_000;
        chain.Add(new Trading.Domain.Entities.OptionChainData
        {
            Symbol = SignalEngineTestFactory.Symbol, TradingSymbol = "NIFTYTEST20050PE", StrikePrice = Spot + 50m,
            OptionType = TradingConst.OptionType.PE, Ltp = 110m, Volume = 300_000,
            OpenInterest = 30_000, Delta = -0.5m, UnderlyingLtp = 0m,
        });

        var signal = RunPut(optionChain: chain).Signal;

        Assert.NotNull(signal);
        Assert.Equal("NIFTYTEST20050PE", signal!.OptionSymbol);
        Assert.Equal(Spot + 50m, signal.StrikePrice);
        Assert.Equal(110m, signal.OptionLtp);
    }

    // ── 13: OI change floor

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
