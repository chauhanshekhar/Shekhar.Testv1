using Microsoft.Extensions.Logging;
using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

// Polling-based market feed for Groww (no WebSocket in Groww Trade API v1).
// Polls /v1/live-data/ohlc every second for subscribed symbols and emits tick JSON
// in the same format as the previous AngelOne WebSocket feed so MarketFeedWorker is unchanged.
public class GrowwMarketFeed : IMarketDataFeed
{
    private readonly IGrowwLiveDataService _liveData;
    private readonly ILogger<GrowwMarketFeed> _logger;

    public event Func<string, Task>? TickReceived;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    // OHLC carries day-scoped fields (open/high/low/volume) that barely move; only LTP changes
    // tick to tick. Every Groww call is serialised behind a process-wide 2.5s gate
    // (GrowwHttpService.SendWithRateLimitAsync), so re-fetching OHLC every cycle spent half the
    // entire API budget on near-static data. Cached and refreshed on this interval instead.
    private static readonly TimeSpan OhlcRefreshInterval = TimeSpan.FromSeconds(30);

    private Dictionary<string, OhlcEntry> _ohlcCache = new();
    private DateTime _ohlcFetchedAtUtc = DateTime.MinValue;

    public GrowwMarketFeed(
        IGrowwLiveDataService liveData,
        ILogger<GrowwMarketFeed> logger)
    {
        _liveData = liveData;
        _logger = logger;
    }

    public async Task StartAsync(IEnumerable<TradingInstrument> tradingInstruments,
        CancellationToken ct)
    {
        var symbolList = tradingInstruments.ToList();

        // Build "EXCHANGE:SYMBOL" keys for the OHLC batch endpoint.
        // For FNO index tracking use the CASH segment LTP equivalent.
        var exchangeSymbols = symbolList
     .Select(s => $"{s.Exchange}_{s.Symbol}")
     .ToList();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // ONE batched call per endpoint for every symbol — not one call per symbol. Both
                // /live-data endpoints accept a list, and `exchangeSymbols` was already being
                // built for exactly that, then discarded in favour of a per-symbol loop. With the
                // 2.5s global rate gate that loop cost 2-3 calls x N symbols per cycle (~15-22s
                // for three indices), which is what starved the option-chain poller and left MFE
                // sampling at ~47s.
                await PollOnceAsync(
                    exchangeSymbols,
                    symbolList.Select(s => (s.Exchange, s.Symbol)).ToList(),
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                try { await Task.Delay(5_000, ct); } catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    public Task StopAsync() => Task.CompletedTask;

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task PollOnceAsync(
    List<string> exchangeSymbols,
    List<(string exchange, string token)> symbolList,
    CancellationToken ct)
    {
        if (ShouldRefreshOhlc(_ohlcFetchedAtUtc, _ohlcCache.Count, DateTime.UtcNow))
        {
            var fresh = await _liveData.GetOhlcAsync(
                GrowwSegment.Cash,
                exchangeSymbols);

            // Keep the previous cache on a failed/empty fetch rather than blanking the day fields;
            // the next cycle retries. The immediate no-backoff re-call that used to sit here just
            // burned another rate-limited slot against the same failure.
            if (fresh.Count > 0)
            {
                _ohlcCache = fresh;
                _ohlcFetchedAtUtc = DateTime.UtcNow;
            }
        }

        var ltpData = await _liveData.GetLtpAsync(
            GrowwSegment.Cash,
            exchangeSymbols);

        foreach (var (exchange, token) in symbolList)
        {
            var key = $"{exchange}_{token}";

            _ohlcCache.TryGetValue(key, out var entry);

            decimal ltp = 0;

            if (ltpData.TryGetValue(key, out var liveLtp))
                ltp = liveLtp;

            // fallback only if LTP API fails
            if (ltp <= 0)
                ltp = entry?.Close ?? 0m;

            // Previously the symbol was skipped whenever OHLC was missing, which suppressed the
            // tick even when a perfectly good LTP had arrived. A live price is the one field the
            // exit path actually needs, so emit on LTP alone and leave the day fields null.
            if (ltp <= 0)
                continue;

            var tickJson = JsonSerializer.Serialize(new
            {
                token = token,
                ltp = ltp,
                open = entry?.Open,
                high = entry?.High,
                low = entry?.Low,
                close = entry?.Close,
                volume = entry?.Volume,
                exchangeTime = IstClock.Now
            });

            await InvokeHandlersAsync(tickJson);
        }
    }

    /// <summary>
    /// OHLC is refreshed on an interval rather than every poll. Pure so the cadence is testable
    /// without a broker client.
    /// </summary>
    internal static bool ShouldRefreshOhlc(DateTime lastFetchedUtc, int cachedCount, DateTime nowUtc)
        => cachedCount == 0 || nowUtc - lastFetchedUtc >= OhlcRefreshInterval;
    private async Task InvokeHandlersAsync(string tickJson)
    {
        var handlers = TickReceived?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try { await ((Func<string, Task>)handler)(tickJson); }
            catch (Exception ex) { _logger.LogError(ex, "GrowwMarketFeed — tick handler threw"); }
        }
    }
}
