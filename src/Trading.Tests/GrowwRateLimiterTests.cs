using Trading.Infrastructure.Brokers.Groww;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Per-category Groww rate limits. Groww: orders 10/s 250/min, live data 10/s 300/min (per account,
/// shared by API + Worker). Defaults per process: orders 5/s 150/min, live data 5/s 200/min, other
/// 3/s 100/min. Replaces a global lock that allowed 24 calls/min for everything, so ticks, chain,
/// exit quotes and orders all queued behind each other.
/// </summary>
public class GrowwRateLimiterTests
{
    private static readonly DateTime T0 = new(2026, 10, 6, 4, 0, 0, DateTimeKind.Utc);

    // ── Classify ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/v1/order/create")]
    [InlineData("/v1/order/cancel")]
    [InlineData("/v1/order/detail/GMKFO123?segment=FNO")]
    [InlineData("/v1/order/status/reference/abc123")]
    [InlineData("/v1/order-advance/create")]
    [InlineData("https://api.groww.in/v1/order/create")]
    public void Classify_Orders(string url)
        => Assert.Equal(GrowwApiCategory.Orders, GrowwRateLimiter.Classify(url));

    [Theory]
    [InlineData("/v1/live-data/quote?exchange=NSE&segment=FNO&trading_symbol=X")]
    [InlineData("/v1/live-data/ltp?segment=CASH&exchange_symbols=NSE_NIFTY")]
    [InlineData("/v1/option-chain/exchange/NSE?underlying=NIFTY&expiry_date=2026-10-07")]
    [InlineData("/v1/historical/candle/range?exchange=NSE")]
    [InlineData("https://api.groww.in/v1/live-data/quote")]
    public void Classify_LiveData(string url)
        => Assert.Equal(GrowwApiCategory.LiveData, GrowwRateLimiter.Classify(url));

    [Theory]
    [InlineData("/v1/positions/user")]
    [InlineData("/v1/holdings/user")]
    [InlineData("/v1/margins/detail/user")]
    [InlineData("/v1/user/detail")]
    [InlineData("")]
    public void Classify_Other(string url)
        => Assert.Equal(GrowwApiCategory.Other, GrowwRateLimiter.Classify(url));

    // ── WaitFor ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WaitFor_UnderBothCaps_NoWait()
        => Assert.Equal(TimeSpan.Zero, GrowwRateLimiter.WaitFor(
            [T0, T0.AddMilliseconds(100)], T0.AddMilliseconds(200), perSecond: 5, perMinute: 200));

    [Fact]
    public void WaitFor_PerSecondCapReached_WaitsForTheOldestInThatSecondToAgeOut()
    {
        var recent = Enumerable.Range(0, 5).Select(i => T0.AddMilliseconds(i * 100)).ToList();   // 0..400 ms
        var wait = GrowwRateLimiter.WaitFor(recent, T0.AddMilliseconds(500), perSecond: 5, perMinute: 200);
        Assert.Equal(TimeSpan.FromMilliseconds(500), wait);                                        // until T0 + 1 s
    }

    [Fact]
    public void WaitFor_PerMinuteCapReached_WaitsForTheMinuteWindow()
    {
        var recent = Enumerable.Range(0, 3).Select(i => T0.AddSeconds(i * 10)).ToList();         // 0, 10, 20 s
        var wait = GrowwRateLimiter.WaitFor(recent, T0.AddSeconds(30), perSecond: 5, perMinute: 3);
        Assert.Equal(TimeSpan.FromSeconds(30), wait);                                              // until T0 + 60 s
    }

    [Fact]
    public void WaitFor_BothCapsReached_TakesTheLongerWait()
    {
        var recent = new List<DateTime> { T0, T0.AddMilliseconds(900) };
        var wait = GrowwRateLimiter.WaitFor(recent, T0.AddMilliseconds(950), perSecond: 2, perMinute: 2);
        Assert.Equal(TimeSpan.FromSeconds(60) - TimeSpan.FromMilliseconds(950), wait);
    }

    [Fact]
    public void WaitFor_OldCallsOutsideTheWindowDoNotCount()
        // Cap 2/min: the 61 s-old call is outside the window, so only one counts -> no wait.
        // (If it were counted, two calls would hit the cap and a wait would be returned.)
        => Assert.Equal(TimeSpan.Zero, GrowwRateLimiter.WaitFor(
            [T0.AddSeconds(-61), T0.AddSeconds(-2)], T0, perSecond: 1, perMinute: 2));

    // ── AcquireAsync (fake clock: the delay advances time) ───────────────────────────────────

    private sealed class FakeTime
    {
        public DateTime Now = T0;
        public readonly List<TimeSpan> Waits = new();
        public Task Delay(TimeSpan d, CancellationToken _) { Waits.Add(d); Now += d; return Task.CompletedTask; }
    }

    private static (GrowwRateLimiter Limiter, FakeTime Time) Make(GrowwRateLimits? limits = null)
    {
        var time = new FakeTime();
        return (new GrowwRateLimiter(limits ?? new GrowwRateLimits(), () => time.Now, time.Delay), time);
    }

    [Fact]
    public async Task SixthLiveDataCallInOneSecondWaits_ButAnOrderDoesNot()
    {
        var (limiter, time) = Make();
        for (var i = 0; i < 5; i++) await limiter.AcquireAsync(GrowwApiCategory.LiveData);
        Assert.Empty(time.Waits);

        await limiter.AcquireAsync(GrowwApiCategory.Orders);                 // own budget: no wait
        Assert.Empty(time.Waits);

        await limiter.AcquireAsync(GrowwApiCategory.LiveData);               // 6th live-data call
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(time.Waits));
    }

    [Fact]
    public async Task PerMinuteCap_HoldsTheNextCallUntilTheWindowClears()
    {
        var (limiter, time) = Make(new GrowwRateLimits { LiveDataPerSecond = 1000, LiveDataPerMinute = 200 });
        for (var i = 0; i < 200; i++) await limiter.AcquireAsync(GrowwApiCategory.LiveData);
        Assert.Empty(time.Waits);

        await limiter.AcquireAsync(GrowwApiCategory.LiveData);               // 201st in the same minute
        Assert.Equal(TimeSpan.FromMinutes(1), time.Waits.Sum(w => w.Ticks) is var t ? TimeSpan.FromTicks(t) : default);
    }

    [Fact]
    public async Task SpacedCalls_NeverWait()
    {
        var (limiter, time) = Make();
        for (var i = 0; i < 120; i++)
        {
            await limiter.AcquireAsync(GrowwApiCategory.LiveData);
            time.Now += TimeSpan.FromMilliseconds(500);                       // 2 calls/s, 120/min: under 5/s and 200/min
        }
        Assert.Empty(time.Waits);
    }

    [Fact]
    public void Defaults_StayBelowGrowwPublishedLimits()
    {
        var d = new GrowwRateLimits();
        Assert.True(d.OrdersPerSecond < 10 && d.OrdersPerMinute < 250);
        Assert.True(d.LiveDataPerSecond < 10 && d.LiveDataPerMinute < 300);
    }
}
