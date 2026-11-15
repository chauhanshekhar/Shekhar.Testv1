using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// AtrRiskService is the ONLY place index StopLoss/Target1/Target2 are computed, and
/// ExitDecisionService exits live positions on those levels. These tests pin the invariant that
/// matters: targets are fixed multiples of the CLAMPED stop distance, so reward/risk is identical
/// whether or not the MinimumIndexStopPoints floor binds.
/// </summary>
public class AtrRiskServiceTests
{
    private static TradingSettings Settings() => new()
    {
        AtrStopLossMultiplier = 1.5m,
        AtrTarget1Multiplier = 1.5m,    // 1.0x risk
        AtrTarget2Multiplier = 1.95m,   // 1.3x risk
        MinimumIndexStopPoints = 20m,
        MaximumIndexStopPoints = 100m,
    };

    private const decimal Entry = 23218.05m;

    // AtrRaw 9.9795 -> raw stop 14.97 -> clamped to 20 (the regime covering 99.1% of snapshots).
    // AtrRaw 40     -> raw stop 60.00 -> unclamped.
    // AtrRaw 80     -> raw stop 120   -> clamped DOWN to the 100 ceiling.
    [Theory]
    [InlineData("9.9795")]
    [InlineData("13.3333")]
    [InlineData("40")]
    [InlineData("80")]
    public void RewardToRisk_IsConstant_AcrossClampRegimes(string atrRaw)
    {
        var atr = decimal.Parse(atrRaw, System.Globalization.CultureInfo.InvariantCulture);
        var risk = new AtrRiskService().CalculateIndexRisk("NIFTY", Entry, atr, isCall: true, Settings());

        var stopDistance = risk.StopLoss - Entry;   // negative for CALL
        var t1Distance = risk.Target1 - Entry;
        var t2Distance = risk.Target2 - Entry;

        Assert.Equal(1.00m, t1Distance / -stopDistance, precision: 4);
        Assert.Equal(1.30m, t2Distance / -stopDistance, precision: 4);
    }

    [Fact]
    public void WhenStopFloorBinds_TargetsWidenWithTheStop()
    {
        // Regression: targets previously used raw ATR while the stop was clamped, leaving
        // index T1 below 1:1 (risk 20.00 to make 19.96) whenever the floor bound.
        var risk = new AtrRiskService().CalculateIndexRisk("NIFTY", Entry, 9.9795m, isCall: false, Settings());

        Assert.Equal(Entry + 20.00m, risk.StopLoss);         // clamped up from 14.97
        Assert.Equal(Entry - 20.00m, risk.Target1);          // 1.0x, NOT the old 19.96
        Assert.Equal(Entry - 26.00m, risk.Target2);          // 1.3x, NOT the old 29.94
    }

    [Fact]
    public void CallAndPut_AreMirrored()
    {
        var s = Settings();
        var call = new AtrRiskService().CalculateIndexRisk("NIFTY", Entry, 40m, isCall: true, s);
        var put = new AtrRiskService().CalculateIndexRisk("NIFTY", Entry, 40m, isCall: false, s);

        Assert.Equal(call.StopLoss - Entry, Entry - put.StopLoss);
        Assert.Equal(call.Target1 - Entry, Entry - put.Target1);
        Assert.Equal(call.Target2 - Entry, Entry - put.Target2);
    }

    [Fact]
    public void ZeroStopMultiplier_FallsBackInsteadOfDividingByZero()
    {
        var s = Settings();
        s.AtrStopLossMultiplier = 0m;

        var risk = new AtrRiskService().CalculateIndexRisk("NIFTY", Entry, 9.9795m, isCall: true, s);

        // Stop collapses to the floor; targets must still be finite and correctly ordered.
        Assert.Equal(Entry - 20.00m, risk.StopLoss);
        Assert.True(risk.Target1 > Entry);
        Assert.True(risk.Target2 > risk.Target1);
    }

    [Fact]
    public void TighterStop_MovesOnlyTheStop_TargetsStayAtAtrMultiples()
    {
        // 2026-10-07 stop-width change 1.5 -> 1.25 (unclamped ATR 40): stop 60 -> 50 points, T1 60 and T2 78 unchanged,
        // so T1 becomes 1.2R and T2 1.56R - RiskRewardGate's 1.3 minimum still holds.
        var live = new AtrRiskService().CalculateIndexRisk("NIFTY", Entry, 40m, isCall: true, Settings());
        var s = Settings(); s.AtrStopLossMultiplier = 1.25m;
        var tight = new AtrRiskService().CalculateIndexRisk("NIFTY", Entry, 40m, isCall: true, s);

        Assert.Equal(Entry - 60m, live.StopLoss);
        Assert.Equal(Entry - 50m, tight.StopLoss);
        Assert.Equal(live.Target1, tight.Target1);
        Assert.Equal(live.Target2, tight.Target2);
    }
}
