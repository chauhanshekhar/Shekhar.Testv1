namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>Groww rate-limit category of an API call.</summary>
public enum GrowwApiCategory
{
    Orders,
    LiveData,
    Other,
}

/// <summary>
/// Per-PROCESS Groww call budgets (config: Groww:RateLimits). Groww's limits apply to the account,
/// shared by the API and Worker processes, so the defaults sit well below them:
///   Orders    - Groww 10/s, 250/min  -> 5/s, 150/min
///   Live data - Groww 10/s, 300/min  -> 5/s, 200/min
///   Other     - not published        -> 3/s, 100/min (cautious)
/// </summary>
public class GrowwRateLimits
{
    public int OrdersPerSecond { get; set; } = 5;
    public int OrdersPerMinute { get; set; } = 150;
    public int LiveDataPerSecond { get; set; } = 5;
    public int LiveDataPerMinute { get; set; } = 200;
    public int OtherPerSecond { get; set; } = 3;
    public int OtherPerMinute { get; set; } = 100;
}

/// <summary>
/// Replaces the old global lock that sent every Groww call one at a time, at least 2.5 s apart
/// (max 24 calls/min for everything - ticks, option chain, candles, exit quotes AND orders), while
/// the Worker wanted ~130/min. Each category now has its own per-second and per-minute window; a
/// caller waits only for a free slot in its own category and then sends without holding any lock,
/// so orders never queue behind market data and calls can be in flight concurrently.
/// </summary>
public sealed class GrowwRateLimiter
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinWait = TimeSpan.FromMilliseconds(1);

    private sealed class Bucket(int perSecond, int perMinute)
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public readonly Queue<DateTime> Recent = new();
        public readonly int PerSecond = Math.Max(1, perSecond);
        public readonly int PerMinute = Math.Max(1, perMinute);
    }

    private readonly Dictionary<GrowwApiCategory, Bucket> _buckets;
    private readonly Func<DateTime> _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public GrowwRateLimiter(
        GrowwRateLimits limits,
        Func<DateTime>? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _buckets = new()
        {
            [GrowwApiCategory.Orders] = new(limits.OrdersPerSecond, limits.OrdersPerMinute),
            [GrowwApiCategory.LiveData] = new(limits.LiveDataPerSecond, limits.LiveDataPerMinute),
            [GrowwApiCategory.Other] = new(limits.OtherPerSecond, limits.OtherPerMinute),
        };
        _clock = clock ?? (() => DateTime.UtcNow);
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Waits until the category has a free slot, then records the call. Callers of the same
    /// category are served in order; the HTTP request itself is sent after this returns.</summary>
    public async Task AcquireAsync(GrowwApiCategory category, CancellationToken ct = default)
    {
        var bucket = _buckets[category];
        await bucket.Gate.WaitAsync(ct);
        try
        {
            while (true)
            {
                var now = _clock();
                while (bucket.Recent.Count > 0 && bucket.Recent.Peek() <= now - Minute)
                    bucket.Recent.Dequeue();

                var wait = WaitFor(bucket.Recent, now, bucket.PerSecond, bucket.PerMinute);
                if (wait <= TimeSpan.Zero)
                {
                    bucket.Recent.Enqueue(now);
                    return;
                }

                await _delay(wait, ct);
            }
        }
        finally
        {
            bucket.Gate.Release();
        }
    }

    /// <summary>
    /// How long until another call fits under both caps, given the recent calls (ascending). Zero when
    /// one is allowed now. With N calls in a window and a cap C, the slot frees when call N-C ages out.
    /// Exposed for testing.
    /// </summary>
    internal static TimeSpan WaitFor(IEnumerable<DateTime> recentAscending, DateTime now, int perSecond, int perMinute)
    {
        var inMinute = recentAscending.Where(t => t > now - Minute).ToList();
        var inSecond = inMinute.Where(t => t > now - Second).ToList();

        var wait = TimeSpan.Zero;
        if (inSecond.Count >= perSecond)
            wait = Max(wait, inSecond[inSecond.Count - perSecond] + Second - now);
        if (inMinute.Count >= perMinute)
            wait = Max(wait, inMinute[inMinute.Count - perMinute] + Minute - now);

        return wait > TimeSpan.Zero ? Max(wait, MinWait) : TimeSpan.Zero;
    }

    /// <summary>Category of a Groww URL (relative path or absolute URL). Exposed for testing.</summary>
    internal static GrowwApiCategory Classify(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            ? absolute.AbsolutePath
            : url.Split('?')[0];

        if (path.StartsWith("/v1/order", StringComparison.OrdinalIgnoreCase))          // order + order-advance
            return GrowwApiCategory.Orders;

        if (path.StartsWith("/v1/live-data", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/v1/option-chain", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/v1/historical", StringComparison.OrdinalIgnoreCase))
            return GrowwApiCategory.LiveData;

        return GrowwApiCategory.Other;                                                   // positions, holdings, margins, user
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
