using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// The opening-conditional RSI band in the LIVE entry chain: a gap-up session can carry a higher
/// CALL band than a flat one. Exercised through the real SignalEngineService via EntryEvaluator, so
/// these assert the shipping behaviour, not a copy of it.
///
/// Default (no EnableOpeningRsiBand row) must reproduce the normal band exactly.
/// </summary>
public class BacktestOpeningRsiBandTests
{
    private static readonly DateTime T = BtSupport.At(5, 30);

    /// <summary>Live-like settings plus the opening-band rows under test.</summary>
    private static BacktestSettings Settings(bool enable, decimal? gapUpFloor = null, decimal? gapUpCeiling = null)
    {
        var s = BtSupport.LiveLike();
        s.Set("CallRsiFloor", null, 45m);
        s.Set("CallRsiCeiling", null, 65m);
        if (enable) s.Set("EnableOpeningRsiBand", null, 1m);
        if (gapUpFloor is { } f) s.Set("GapUpCallRsiFloor", null, f);
        if (gapUpCeiling is { } c) s.Set("GapUpCallRsiCeiling", null, c);
        return s;
    }

    private static bool CallPasses(BacktestSettings settings, double rsi5, double gapPct)
    {
        var baseline = BtSupport.CallPoint("BANKNIFTY", T, rsi5: rsi5);
        // DayOpen and SessionOpen are the same bar open in the real series (MarketSeriesBuilder),
        // and the YesterdayClose used by the band is derived from SessionOpen + OpenGapPct - so the
        // two must agree here or the recomputed gap will not match OpenGapPct.
        var p = baseline with
        {
            OpenGapPct = gapPct,
            SessionOpen = baseline.DayOpen,
        };
        var ev = new EntryEvaluator(settings, BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore);
        return ev.Evaluate(p, TradeSide.Call).Candidate;
    }

    [Fact]
    public void WithTheFeatureOff_theNormalBandApplies_onEverySession()
    {
        var off = Settings(enable: false, gapUpFloor: 45m, gapUpCeiling: 85m);

        Assert.True(CallPasses(off, rsi5: 60, gapPct: 0.80));    // inside 45-65
        Assert.False(CallPasses(off, rsi5: 75, gapPct: 0.80));   // above 65, and the override is OFF
    }

    [Fact]
    public void OnAGapUpSession_theOverrideBandApplies()
    {
        var on = Settings(enable: true, gapUpFloor: 45m, gapUpCeiling: 85m);

        Assert.True(CallPasses(on, rsi5: 75, gapPct: 0.80));     // 75 is inside the 45-85 gap-up band
        Assert.False(CallPasses(on, rsi5: 90, gapPct: 0.80));    // still above the override ceiling
    }

    [Fact]
    public void OnANormalOpen_theNormalBandStillApplies()
    {
        var on = Settings(enable: true, gapUpFloor: 45m, gapUpCeiling: 85m);

        Assert.False(CallPasses(on, rsi5: 75, gapPct: 0.05));    // normal open keeps 45-65
        Assert.True(CallPasses(on, rsi5: 60, gapPct: 0.05));
    }

    [Fact]
    public void OnAGapDownSession_theGapUpOverrideIsNotUsed()
    {
        var on = Settings(enable: true, gapUpFloor: 45m, gapUpCeiling: 85m);
        Assert.False(CallPasses(on, rsi5: 75, gapPct: -0.80));
    }

    [Fact]
    public void AHalfConfiguredOverride_isIgnored_ratherThanWideningOneEnd()
    {
        // Ceiling row present, floor row absent: the pair is incomplete, so the normal band stands.
        var half = Settings(enable: true, gapUpCeiling: 85m);
        Assert.False(CallPasses(half, rsi5: 75, gapPct: 0.80));
    }

    [Fact]
    public void TheGapThreshold_decidesWhichBandIsUsed()
    {
        var on = Settings(enable: true, gapUpFloor: 45m, gapUpCeiling: 85m);

        Assert.False(CallPasses(on, rsi5: 75, gapPct: 0.20));    // below 0.30% = normal open
        Assert.True(CallPasses(on, rsi5: 75, gapPct: 0.31));     // at/above = gap-up
    }
}
