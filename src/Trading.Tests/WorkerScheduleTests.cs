using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Data-fetch schedules. Measured 2026-10-01/05: a closed 1m candle reached the signal snapshot a
/// median ~30 s late (HistoricalDataWorker slept 60 s after each run, at a random phase), and the
/// option chain refreshed every ~50 s (OptionChainWorker slept 30 s after fetching). Candles are now
/// fetched a few seconds after each minute boundary; the chain runs on a fixed 30 s start-to-start.
/// </summary>
public class WorkerScheduleTests
{
    private static readonly TimeSpan Offset3 = TimeSpan.FromSeconds(3);
    private static DateTime At(int h, int m, int s, int ms = 0) => new(2026, 10, 6, h, m, s, ms, DateTimeKind.Utc);

    // ── HistoricalDataWorker.RefreshFromIst ─────────────────────────────────────────────────

    private static readonly DateTime SessionOpenIst = new(2026, 10, 12, 9, 15, 0);

    [Fact]
    public void RefreshWindow_UsesIstTime_NotTheUtcCandleTime()
    {
        // Last stored 1m candle opened 08:25 UTC = 13:55 IST. The 5-minute window starts 13:50 IST, not session open.
        var last = new DateTime(2026, 10, 12, 8, 25, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 10, 12, 13, 50, 0), HistoricalDataWorker.RefreshFromIst(last, 5, SessionOpenIst));
    }

    [Fact]
    public void RefreshWindow_NeverStartsBeforeSessionOpen()
    {
        // 03:46 UTC = 09:16 IST; 5 minutes back would be 09:11 IST, so it is clamped to 09:15.
        var last = new DateTime(2026, 10, 12, 3, 46, 0, DateTimeKind.Utc);

        Assert.Equal(SessionOpenIst, HistoricalDataWorker.RefreshFromIst(last, 5, SessionOpenIst));
    }

    // ── HistoricalDataWorker.ShouldTryFallbackContract ──────────────────────────────────────

    [Fact]
    public void FallbackContract_NotUsed_WhenSeriesAlreadyHasBarsToday()
    {
        // SENSEX 2026-10-26 11:49: the thin Nov contract returned nothing for one window; the Dec fallback then wrote
        // a different running volume into the same series and killed the day's VWAP.
        var now = new DateTime(2026, 10, 26, 11, 49, 30);
        var open = new DateTime(2026, 10, 26, 9, 15, 0);

        Assert.False(HistoricalDataWorker.ShouldTryFallbackContract(0, now, open, hasBarsToday: true));
    }

    [Fact]
    public void FallbackContract_Used_WhenNearMonthIsEmptyAndDayHasNoBars()
    {
        var now = new DateTime(2026, 10, 26, 9, 20, 0);
        var open = new DateTime(2026, 10, 26, 9, 15, 0);

        Assert.True(HistoricalDataWorker.ShouldTryFallbackContract(0, now, open, hasBarsToday: false));
    }

    [Fact]
    public void FallbackContract_NotUsed_WhenNearMonthReturnedCandles_OrInFirstThreeMinutes()
    {
        var open = new DateTime(2026, 10, 26, 9, 15, 0);

        Assert.False(HistoricalDataWorker.ShouldTryFallbackContract(12, open.AddMinutes(10), open, hasBarsToday: false));
        Assert.False(HistoricalDataWorker.ShouldTryFallbackContract(0, open.AddMinutes(2), open, hasBarsToday: false));
    }

    // ── HistoricalDataWorker.DelayUntilNextMinute ───────────────────────────────────────────

    [Fact]
    public void MidMinute_WaitsUntilNextMinutePlusOffset()
        => Assert.Equal(TimeSpan.FromSeconds(23), HistoricalDataWorker.DelayUntilNextMinute(At(4, 30, 40), Offset3));

    [Fact]
    public void JustAfterBoundary_BeforeOffset_FiresThisMinute()
        => Assert.Equal(TimeSpan.FromSeconds(2), HistoricalDataWorker.DelayUntilNextMinute(At(4, 30, 1), Offset3));

    [Fact]
    public void ExactlyAtTheOffset_WaitsAFullMinute()
        => Assert.Equal(TimeSpan.FromMinutes(1), HistoricalDataWorker.DelayUntilNextMinute(At(4, 30, 3), Offset3));

    [Fact]
    public void EndOfMinute_FractionalSeconds()
        => Assert.Equal(TimeSpan.FromMilliseconds(3500), HistoricalDataWorker.DelayUntilNextMinute(At(4, 30, 59, 500), Offset3));

    [Fact]
    public void ZeroOffset_WaitsForTheBoundary()
        => Assert.Equal(TimeSpan.FromSeconds(30), HistoricalDataWorker.DelayUntilNextMinute(At(4, 30, 30), TimeSpan.Zero));

    [Fact]
    public void DelayIsAlwaysPositiveAndAtMostOneMinute()
    {
        for (var ms = 0; ms < 60_000; ms += 250)
        {
            var d = HistoricalDataWorker.DelayUntilNextMinute(At(4, 30, 0).AddMilliseconds(ms), Offset3);
            Assert.InRange(d, TimeSpan.FromTicks(1), TimeSpan.FromMinutes(1));
        }
    }

    // ── OptionChainWorker.NextDelay ─────────────────────────────────────────────────────────

    private static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(0, 30_000)]
    [InlineData(20, 10_000)]
    [InlineData(30, 1_000)]     // cycle used the whole period: minimum pause
    [InlineData(45, 1_000)]     // overran: minimum pause, never a tight loop
    public void NextDelay_KeepsA30sStartToStartSchedule(int elapsedSeconds, int expectedMs)
        => Assert.Equal(TimeSpan.FromMilliseconds(expectedMs),
            OptionChainWorker.NextDelay(Period, TimeSpan.FromSeconds(elapsedSeconds)));

    // ── OptionChainWorker.RefreshPeriod (OptionChain:RefreshSeconds) ────────────────────────
    // One cycle takes a median 10 s (p90 17.5 s, 2026-10-05), so the default is 15 s.

    [Fact]
    public void RefreshPeriod_NoSetting_Is15s()
        => Assert.Equal(TimeSpan.FromSeconds(15), OptionChainWorker.RefreshPeriod(null));

    [Theory]
    [InlineData(1, 5)]      // below the floor -> 5 s
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    [InlineData(300, 60)]   // above the ceiling -> 60 s
    public void RefreshPeriod_IsClampedTo5To60s(int configured, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), OptionChainWorker.RefreshPeriod(configured));

    [Theory]
    [InlineData(10, 5_000)]     // 15 s default, 10 s median cycle -> 5 s pause
    [InlineData(17, 1_000)]     // p90 cycle overruns 15 s -> minimum pause
    public void NextDelay_With15sDefault(int elapsedSeconds, int expectedMs)
        => Assert.Equal(TimeSpan.FromMilliseconds(expectedMs),
            OptionChainWorker.NextDelay(OptionChainWorker.RefreshPeriod(null), TimeSpan.FromSeconds(elapsedSeconds)));
}
