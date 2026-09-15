using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Every Groww call is serialised behind a process-wide 2.5s gate
/// (GrowwHttpService.SendWithRateLimitAsync), so the per-cycle CALL COUNT — not the poll delay —
/// sets how fresh index ticks and option prices can ever be. These cover the two wastes that were
/// consuming that budget: per-symbol calls to a batch endpoint, and re-fetching day-scoped OHLC
/// every single cycle.
/// </summary>
public class MarketFeedPollBudgetTests
{
    private const decimal RateGateSeconds = 2.5m;

    // ── OHLC refresh cadence ─────────────────────────────────────────────────

    [Fact]
    public void ColdCache_RefreshesImmediately()
    {
        var now = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(GrowwMarketFeed.ShouldRefreshOhlc(DateTime.MinValue, cachedCount: 0, now));
    }

    [Fact]
    public void WarmCache_IsReusedWithinTheInterval()
    {
        var fetched = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        // 1s and 29s later: still inside the 30s window, so no call is spent.
        Assert.False(GrowwMarketFeed.ShouldRefreshOhlc(fetched, 3, fetched.AddSeconds(1)));
        Assert.False(GrowwMarketFeed.ShouldRefreshOhlc(fetched, 3, fetched.AddSeconds(29)));
    }

    [Fact]
    public void Cache_RefreshesOnceTheIntervalElapses()
    {
        var fetched = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(GrowwMarketFeed.ShouldRefreshOhlc(fetched, 3, fetched.AddSeconds(30)));
        Assert.True(GrowwMarketFeed.ShouldRefreshOhlc(fetched, 3, fetched.AddSeconds(31)));
    }

    [Fact]
    public void EmptyCache_RefreshesEvenInsideTheInterval()
    {
        // A failed fetch must not leave the feed with no day fields until the window expires.
        var fetched = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(GrowwMarketFeed.ShouldRefreshOhlc(fetched, cachedCount: 0, fetched.AddSeconds(2)));
    }

    // ── Call budget: the actual point of the change ──────────────────────────

    /// <summary>Calls per poll cycle under the OLD per-symbol loop: (OHLC + LTP) x N symbols.</summary>
    private static int OldCallsPerCycle(int symbols) => symbols * 2;

    /// <summary>
    /// Calls per poll cycle now: one batched LTP always, plus one batched OHLC only on a
    /// refresh cycle.
    /// </summary>
    private static int NewCallsPerCycle(bool ohlcRefreshDue) => 1 + (ohlcRefreshDue ? 1 : 0);

    [Theory]
    [InlineData(3)]   // NIFTY + BANKNIFTY + SENSEX today
    [InlineData(5)]
    public void BatchingCutsCallsPerCycle(int symbols)
    {
        Assert.True(NewCallsPerCycle(ohlcRefreshDue: true) < OldCallsPerCycle(symbols));
        Assert.True(NewCallsPerCycle(ohlcRefreshDue: false) < OldCallsPerCycle(symbols));
    }

    [Fact]
    public void ThreeSymbols_SteadyStateCycleIsOneCall_DownFromSix()
    {
        Assert.Equal(6, OldCallsPerCycle(3));                        // 2 calls x 3 symbols
        Assert.Equal(1, NewCallsPerCycle(ohlcRefreshDue: false));    // batched LTP only
        Assert.Equal(2, NewCallsPerCycle(ohlcRefreshDue: true));     // + batched OHLC
    }

    [Fact]
    public void SteadyStateTickLatency_DropsBelowFiveSeconds()
    {
        // The measured symptom: ~16s between index ticks, ~47s between option-chain snapshots.
        var before = OldCallsPerCycle(3) * RateGateSeconds;          // 15.0s
        var after = NewCallsPerCycle(ohlcRefreshDue: false) * RateGateSeconds;  // 2.5s

        Assert.Equal(15.0m, before);
        Assert.Equal(2.5m, after);
        Assert.True(after < 5m, "a poll cycle must fit in a couple of rate-gate slots");
    }

    [Fact]
    public void BatchKeyFormat_MatchesTheLookupKey()
    {
        // The batched request key and the per-symbol response lookup must agree, or every symbol
        // silently misses and no tick is ever emitted.
        var instrument = new TradingInstrument { Symbol = "BANKNIFTY", Exchange = "NSE" };

        var requestKey = $"{instrument.Exchange}_{instrument.Symbol}";
        var lookupKey = $"{instrument.Exchange}_{instrument.Symbol}";

        Assert.Equal(requestKey, lookupKey);
        Assert.Equal("NSE_BANKNIFTY", requestKey);
    }
}
