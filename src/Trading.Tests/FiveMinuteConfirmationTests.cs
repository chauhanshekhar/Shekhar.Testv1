using Trading.Infrastructure;
using Trading.SignalEngine.Strategies;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// 5-minute confirmation + 1-minute entry.
///
///     5m -> may we trade this direction at all?
///     1m -> is this the moment?   (unchanged existing logic, still the final trigger)
///
/// The confirmation is a VETO, never a trigger: passing it never creates a signal on its own, so
/// a 5m-bullish market with no 1m trigger produces nothing. Cases 3-6 below pin that both must
/// agree, and cases 7-8 pin that a still-forming bar can reach neither.
/// </summary>
public class FiveMinuteConfirmationTests
{
    private static readonly FiveMinuteConfirmationService Service = new();

    /// <summary>Stock settings — RsiBullConfirm 58 / RsiBearConfirm 42, both reused as-is.</summary>
    private static TradingSettings Settings() => new();

    private const decimal BullishEma20 = 57_150m;   // EMA20_5m above EMA50_5m
    private const decimal BullishEma50 = 57_100m;
    private const decimal BearishEma20 = 57_100m;   // EMA20_5m below EMA50_5m
    private const decimal BearishEma50 = 57_150m;

    // ── CASE 1: 5m bullish confirmation → CALL allowed ──────────────────────────────────────

    [Fact]
    public void Case1_FiveMinuteBullish_ConfirmsCall()
    {
        var r = Service.Confirm(call: true, BullishEma20, BullishEma50, rsi5m: 65m, Settings());

        Assert.True(r.Passed);
        Assert.Equal("Bullish", r.TrendDirection5m);
        Assert.Null(r.Reason);
    }

    // ── CASE 2: 5m bearish confirmation → PUT allowed ───────────────────────────────────────

    [Fact]
    public void Case2_FiveMinuteBearish_ConfirmsPut()
    {
        var r = Service.Confirm(call: false, BearishEma20, BearishEma50, rsi5m: 30m, Settings());

        Assert.True(r.Passed);
        Assert.Equal("Bearish", r.TrendDirection5m);
    }

    // ── CASE 3 & 4: conflicting timeframes must be rejected ─────────────────────────────────
    // The 1m side produced a signal (that is why we are here); the 5m environment disagrees.

    [Fact]
    public void Case3_FiveMinuteBullish_RejectsAPutFromTheOneMinuteTrigger()
    {
        var r = Service.Confirm(call: false, BullishEma20, BullishEma50, rsi5m: 65m, Settings());

        Assert.False(r.Passed);
        Assert.Contains("Bullish", r.Reason);
    }

    [Fact]
    public void Case4_FiveMinuteBearish_RejectsACallFromTheOneMinuteTrigger()
    {
        var r = Service.Confirm(call: true, BearishEma20, BearishEma50, rsi5m: 30m, Settings());

        Assert.False(r.Passed);
        Assert.Contains("Bearish", r.Reason);
    }

    // ── CASE 5: confirmation fails → no entry regardless of the 1m trigger ──────────────────

    [Fact]
    public void Case5_SidewaysFiveMinute_BlocksBothDirections()
    {
        // EMA20_5m == EMA50_5m -> neither direction is supported.
        Assert.False(Service.Confirm(true, 57_100m, 57_100m, 60m, Settings()).Passed);
        Assert.False(Service.Confirm(false, 57_100m, 57_100m, 40m, Settings()).Passed);
    }

    [Fact]
    public void Case5_CorrectTrendButRsiDisagrees_StillBlocks()
    {
        // 5m trend is bullish but 5m RSI has not confirmed it (below RsiBullConfirm 58).
        var call = Service.Confirm(true, BullishEma20, BullishEma50, rsi5m: 50m, Settings());
        Assert.False(call.Passed);
        Assert.Contains("RsiBullConfirm", call.Reason);

        // Mirror for PUT: bearish trend, RSI above RsiBearConfirm 42.
        var put = Service.Confirm(false, BearishEma20, BearishEma50, rsi5m: 50m, Settings());
        Assert.False(put.Passed);
        Assert.Contains("RsiBearConfirm", put.Reason);
    }

    // ── CASE 6 is a StrategyManager-level property ──────────────────────────────────────────
    // "confirmation passes but the 1m trigger fails -> no entry" holds structurally: the gate is
    // only reached AFTER an engine produced a Signal, so no 1m trigger means no Signal to gate.
    // Confirming never creates one.

    [Fact]
    public void Case6_ConfirmationAloneNeverProducesADirection()
    {
        // A pass is permission, not a signal — the same 5m state confirms CALL and denies PUT,
        // and neither outcome carries an entry.
        var call = Service.Confirm(true, BullishEma20, BullishEma50, 65m, Settings());
        var put = Service.Confirm(false, BullishEma20, BullishEma50, 65m, Settings());

        Assert.True(call.Passed);
        Assert.False(put.Passed);
    }

    // ── CASE 7 & 8: a still-forming candle must never be used ───────────────────────────────

    [Theory]
    [InlineData(5)]
    [InlineData(1)]
    public void Case7And8_FormingBarIsExcluded(int barMinutes)
    {
        var now = new DateTime(2026, 8, 19, 10, 43, 0, DateTimeKind.Unspecified);

        // The bar that opened barMinutes ago has NOT finished forming at `now`.
        var formingBar = now.AddMinutes(-barMinutes).AddSeconds(1);
        Assert.False(MarketSnapshotBuilder.IsClosedBar(formingBar, barMinutes, now));

        // The bar that closed exactly at `now` is usable.
        var justClosed = now.AddMinutes(-barMinutes);
        Assert.True(MarketSnapshotBuilder.IsClosedBar(justClosed, barMinutes, now));
    }

    [Fact]
    public void Case7_AtTenFortyThree_TheTenFortyToTenFortyFiveBarIsNotUsed()
    {
        // The worked example from the spec.
        var now = new DateTime(2026, 8, 19, 10, 43, 0, DateTimeKind.Unspecified);

        var forming = new DateTime(2026, 8, 19, 10, 40, 0, DateTimeKind.Unspecified);
        var lastClosed = new DateTime(2026, 8, 19, 10, 35, 0, DateTimeKind.Unspecified);

        Assert.False(MarketSnapshotBuilder.IsClosedBar(forming, 5, now));
        Assert.True(MarketSnapshotBuilder.IsClosedBar(lastClosed, 5, now));
    }

    // ── Fail closed when the 5m series is not ready ─────────────────────────────────────────

    [Fact]
    public void MissingFiveMinuteRsi_FailsClosed()
    {
        // Rsi5m is null for roughly the first 75 minutes of a session. "Cannot confirm" is not
        // "confirmed" — this blocks rather than waving the trade through.
        var r = Service.Confirm(true, BullishEma20, BullishEma50, rsi5m: null, Settings());

        Assert.False(r.Passed);
        Assert.Contains("5m RSI not available", r.Reason);
    }

    [Fact]
    public void MissingFiveMinuteEmas_FailsClosed()
    {
        // EMA20_5m/EMA50_5m are 0 until 20 closed 5m bars exist (~100 minutes in).
        var r = Service.Confirm(true, 0m, 0m, rsi5m: 65m, Settings());

        Assert.False(r.Passed);
        Assert.Contains("5m EMAs not available", r.Reason);
    }

    // ── Derived 5m TrendDirection uses the same expression as the 1m one ────────────────────

    [Theory]
    [InlineData(101, 100, "Bullish")]
    [InlineData(100, 101, "Bearish")]
    [InlineData(100, 100, "Sideways")]
    public void TrendDirectionMirrorsTheOneMinuteExpression(int ema20, int ema50, string expected)
    {
        Assert.Equal(expected, FiveMinuteConfirmationService.TrendDirection(ema20, ema50));
    }

    // ── Thresholds are the EXISTING settings, not new numbers ───────────────────────────────

    [Fact]
    public void ConfirmationUsesExistingRsiConfirmSettings()
    {
        var settings = Settings();
        Assert.Equal(58m, settings.RsiBullConfirm);
        Assert.Equal(42m, settings.RsiBearConfirm);

        // Exactly at the threshold confirms; a hair beyond it does not.
        Assert.True(Service.Confirm(true, BullishEma20, BullishEma50, 58m, settings).Passed);
        Assert.False(Service.Confirm(true, BullishEma20, BullishEma50, 57.99m, settings).Passed);

        Assert.True(Service.Confirm(false, BearishEma20, BearishEma50, 42m, settings).Passed);
        Assert.False(Service.Confirm(false, BearishEma20, BearishEma50, 42.01m, settings).Passed);
    }
}
