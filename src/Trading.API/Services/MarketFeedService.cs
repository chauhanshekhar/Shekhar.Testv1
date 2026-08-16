using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;

namespace Trading.API.Services;

// Broadcasts live ticks to Angular via SignalR.
// Connection lifecycle (auth, connect, subscribe, reconnect) is handled by IMarketDataFeed.
public class MarketFeedService : BackgroundService
{
    private readonly IMarketDataFeed _feed;
    private readonly INotificationService _notifier;
    private readonly ILogger<MarketFeedService> _logger;
    private readonly IInstrumentCache _instrumentCache;

    public MarketFeedService(
        IMarketDataFeed feed,
        INotificationService notifier,
        ILogger<MarketFeedService> logger, IInstrumentCache instrumentCache)
    {
        _feed = feed;
        _notifier = notifier;
        _logger = logger;
        _instrumentCache = instrumentCache;

        _feed.TickReceived += OnTick;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var instruments = _instrumentCache.GetActive();

        if (instruments.Count == 0)
        {
            return;
        }
        await _feed.StartAsync(instruments.ToList(), stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _feed.TickReceived -= OnTick;
        await _feed.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task OnTick(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ltp", out var ltpEl)) return;
            if (!decimal.TryParse(ltpEl.ToString(), out var ltp)) return;

            var tokenStr = root.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "";
          
            var instruments = _instrumentCache.GetActive();
            var symbol = instruments!.FirstOrDefault(s => s.Symbol == tokenStr)!.Symbol ?? tokenStr;
            decimal open = ltp, high = ltp, low = ltp, close = ltp;
            long volume = 0;

            if (root.TryGetProperty("open", out var oEl) && decimal.TryParse(oEl.ToString(), out var o) && o > 0) open = o;
            if (root.TryGetProperty("high", out var hEl) && decimal.TryParse(hEl.ToString(), out var h) && h > 0) high = h;
            if (root.TryGetProperty("low", out var lEl) && decimal.TryParse(lEl.ToString(), out var l) && l > 0) low = l;
            if (root.TryGetProperty("close", out var cEl) && decimal.TryParse(cEl.ToString(), out var c) && c > 0) close = c;
            if (root.TryGetProperty("volume", out var vEl) && long.TryParse(vEl.ToString(), out var v)) volume = v;

            await _notifier.BroadcastTickAsync(new { token = tokenStr, symbol, ltp, open, high, low, close, volume });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarketFeedService OnTick error");
        }
    }
}
