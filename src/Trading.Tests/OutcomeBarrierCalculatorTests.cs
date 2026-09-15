using Trading.Domain;
using Trading.Domain.Entities;
using Trading.SignalEngine.Analytics;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// +20 / -20 INDEX-point first-hit logic for IndicatorSnapshot outcome tracking.
///
/// These are index points on IndicatorSnapshot.Ltp — NOT option premium. Nothing here relates to
/// OptionSL/OptionT1/OptionT2 or Delta.
///
/// The cases that matter most are first-hit ordering (a later opposite barrier must not overwrite
/// an earlier one, because the trade would already have exited) and the "not due yet" state, which
/// must never be recorded as NONE.
/// </summary>
public class OutcomeBarrierCalculatorTests
{
    private static readonly DateTime Entry = new(2026, 8, 19, 10, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Far enough past the entry that all three windows have elapsed.</summary>
    private static readonly DateTime WellAfter = Entry.AddMinutes(30);

    private const decimal EntryPrice = 57_000m;

    /// <summary>Prices one minute apart starting at entry+1m.</summary>
    private static FuturePoint[] Path(params decimal[] prices) =>
        prices.Select((p, i) => new FuturePoint(Entry.AddMinutes(i + 1), p)).ToArray();

    private static OutcomeCalculation Calc(string direction, FuturePoint[] future, DateTime? asOf = null) =>
        OutcomeBarrierCalculator.Calculate(direction, EntryPrice, Entry, future, asOf ?? WellAfter);

    // ── CALL ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Call_Up20Points_IsTarget()
    {
        var r = Calc(TradingConst.SignalType.CALL, Path(57_005m, 57_012m, 57_021m, 56_995m));

        Assert.Equal(OutcomeResult.Target, r.Window15.Result);
        Assert.True(r.Window15.TargetHit);
        Assert.False(r.Window15.SlHit);
    }

    [Fact]
    public void Call_Down20Points_IsStopLoss()
    {
        var r = Calc(TradingConst.SignalType.CALL, Path(56_995m, 56_988m, 56_980m));

        Assert.Equal(OutcomeResult.StopLoss, r.Window15.Result);
        Assert.True(r.Window15.SlHit);
        Assert.False(r.Window15.TargetHit);
    }

    // ── PUT — direction is inverted: the INDEX must FALL for a PUT to win ────────────────────

    [Fact]
    public void Put_IndexFalls20Points_IsTarget()
    {
        // 57000 -> 56980 is -20 on the index, which is +20 FOR the put.
        var r = Calc(TradingConst.SignalType.PUT, Path(57_005m, 56_995m, 56_980m, 57_010m));

        Assert.Equal(OutcomeResult.Target, r.Window15.Result);
    }

    [Fact]
    public void Put_IndexRises20Points_IsStopLoss()
    {
        var r = Calc(TradingConst.SignalType.PUT, Path(57_005m, 57_015m, 57_020m));

        Assert.Equal(OutcomeResult.StopLoss, r.Window15.Result);
    }

    // ── Inclusive boundaries: >= +20 and <= -20 ─────────────────────────────────────────────

    [Theory]
    [InlineData("57019.99", null)]              // +19.99 — not yet TARGET
    [InlineData("57020.00", OutcomeResult.Target)]
    [InlineData("56980.01", null)]              // -19.99 — not yet SL
    [InlineData("56980.00", OutcomeResult.StopLoss)]
    public void Call_BarrierIsInclusive(string price, string? expectedHit)
    {
        var p = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        var r = Calc(TradingConst.SignalType.CALL, Path(p));

        Assert.Equal(expectedHit ?? OutcomeResult.None, r.Window15.Result);
    }

    // ── First-hit ordering — the whole point of the feature ─────────────────────────────────

    [Fact]
    public void TargetFirstThenStop_StaysTarget()
    {
        // Would have exited at TARGET; the later -20 never happens for this trade.
        var r = Calc(TradingConst.SignalType.CALL, Path(57_021m, 57_000m, 56_975m));

        Assert.Equal(OutcomeResult.Target, r.Window15.Result);
        Assert.False(r.Window15.SlHit);
    }

    [Fact]
    public void StopFirstThenTarget_StaysStopLoss()
    {
        var r = Calc(TradingConst.SignalType.CALL, Path(56_979m, 57_000m, 57_030m));

        Assert.Equal(OutcomeResult.StopLoss, r.Window15.Result);
        Assert.False(r.Window15.TargetHit);
    }

    [Fact]
    public void NeitherBarrierReached_IsNone()
    {
        var r = Calc(TradingConst.SignalType.CALL, Path(57_005m, 56_990m, 57_010m, 56_995m));

        Assert.Equal(OutcomeResult.None, r.Window15.Result);
        Assert.False(r.Window15.TargetHit);
        Assert.False(r.Window15.SlHit);
    }

    // ── Each window evaluates the path from entry to its own end, independently ──────────────

    [Fact]
    public void HitAfterFiveMinutes_IsNoneAt5ButTargetAt10And15()
    {
        // Barrier reached at entry+7m only.
        var future = new[]
        {
            new FuturePoint(Entry.AddMinutes(3), 57_010m),
            new FuturePoint(Entry.AddMinutes(7), 57_025m),
        };

        var r = Calc(TradingConst.SignalType.CALL, future);

        Assert.Equal(OutcomeResult.None, r.Window5.Result);
        Assert.Equal(OutcomeResult.Target, r.Window10.Result);
        Assert.Equal(OutcomeResult.Target, r.Window15.Result);
    }

    [Fact]
    public void HitAfterTenMinutes_IsNoneAt5And10ButSlAt15()
    {
        var future = new[]
        {
            new FuturePoint(Entry.AddMinutes(4), 56_990m),
            new FuturePoint(Entry.AddMinutes(12), 56_975m),
        };

        var r = Calc(TradingConst.SignalType.CALL, future);

        Assert.Equal(OutcomeResult.None, r.Window5.Result);
        Assert.Equal(OutcomeResult.None, r.Window10.Result);
        Assert.Equal(OutcomeResult.StopLoss, r.Window15.Result);
    }

    [Fact]
    public void PriceOutsideFifteenMinutes_IsIgnored()
    {
        var future = new[] { new FuturePoint(Entry.AddMinutes(16), 57_100m) };

        var r = Calc(TradingConst.SignalType.CALL, future);

        Assert.Equal(OutcomeResult.None, r.Window15.Result);
    }

    // ── Pending: a window that has not elapsed must be null, never NONE ─────────────────────

    [Fact]
    public void WindowNotYetElapsed_IsPendingNotNone()
    {
        // Only 6 minutes have passed: the 5m window is due, 10m and 15m are not.
        var r = Calc(TradingConst.SignalType.CALL, Path(57_005m), asOf: Entry.AddMinutes(6));

        Assert.Equal(OutcomeResult.None, r.Window5.Result);   // due, and nothing was hit
        Assert.Null(r.Window10.Result);                        // not due -> pending
        Assert.Null(r.Window15.Result);
        Assert.Null(r.Window15.MaxFavorable);
        Assert.Equal(5, r.CompletedWindowMinutes);
    }

    [Fact]
    public void NoWindowElapsed_NothingIsClassified()
    {
        var r = Calc(TradingConst.SignalType.CALL, Path(57_005m), asOf: Entry.AddMinutes(2));

        Assert.Null(r.Window5.Result);
        Assert.Equal(0, r.CompletedWindowMinutes);
    }

    [Fact]
    public void NoFutureDataAtAll_ButWindowElapsed_IsNone()
    {
        var r = Calc(TradingConst.SignalType.CALL, []);

        Assert.Equal(OutcomeResult.None, r.Window15.Result);
        Assert.Null(r.Window15.MaxFavorable);   // no data to measure, not a fabricated zero
        Assert.Equal(15, r.CompletedWindowMinutes);
    }

    // ── Excursions ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Excursions_AreDirectionalAndSpanTheWholeWindow()
    {
        // CALL: best +25 at the peak, worst -15 at the trough.
        var r = Calc(TradingConst.SignalType.CALL, Path(57_010m, 57_025m, 56_985m));

        Assert.Equal(25m, r.Window15.MaxFavorable);
        Assert.Equal(-15m, r.Window15.MaxAdverse);
    }

    [Fact]
    public void Excursions_ArePositiveWhenFavorableForPut()
    {
        // PUT: index falling is favourable, so 57000 -> 56985 is +15 for the put.
        var r = Calc(TradingConst.SignalType.PUT, Path(57_010m, 56_985m));

        Assert.Equal(15m, r.Window15.MaxFavorable);
        Assert.Equal(-10m, r.Window15.MaxAdverse);
    }

    // ── Anomaly: contradictory data is flagged, never guessed ───────────────────────────────

    [Fact]
    public void BothBarriersAtSameTimestamp_IsFlaggedAndNotClassified()
    {
        var t = Entry.AddMinutes(2);
        var future = new[]
        {
            new FuturePoint(t, 57_025m),   // TARGET
            new FuturePoint(t, 56_975m),   // SL, same instant — duplicate/corrupt data
        };

        var r = Calc(TradingConst.SignalType.CALL, future);

        Assert.NotNull(r.Anomaly);
        Assert.Contains("Both TARGET and SL", r.Anomaly);
        Assert.Null(r.Window15.Result);     // deliberately unclassified
    }

    // ── Direction mapping ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Bullish", TradingConst.SignalType.CALL)]
    [InlineData("bullish", TradingConst.SignalType.CALL)]
    [InlineData("Bearish", TradingConst.SignalType.PUT)]
    [InlineData("bearish", TradingConst.SignalType.PUT)]
    public void TrendDirection_MapsToTradedDirection(string trend, string expected)
    {
        Assert.Equal(expected, OutcomeBarrierCalculator.DirectionFromTrend(trend));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Sideways")]
    [InlineData("Unknown")]
    public void UnusableTrendDirection_YieldsNoDirection(string? trend)
    {
        // Null means the caller must skip and log — never invent a direction.
        Assert.Null(OutcomeBarrierCalculator.DirectionFromTrend(trend));
    }
}
