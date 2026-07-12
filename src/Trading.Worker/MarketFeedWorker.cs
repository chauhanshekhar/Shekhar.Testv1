using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;

namespace Trading.Worker;

// Persists live market ticks to the database.
// Connection lifecycle (auth, connect, subscribe, reconnect) is handled by IMarketDataFeed.
public class MarketFeedWorker : BackgroundService
{
    private readonly IMarketDataFeed _feed;
    private readonly ILogger<MarketFeedWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradingAlertService _alerts;
    private readonly IInstrumentCache _instrumentCache;

    private readonly Channel<MarketTick> _tickChannel;

    private long _droppedTicks;
    private long _droppedTicksTotal;
    private const int MaxBatchSize = 500;
    private const int DropAlertThreshold = 100;   // alert if >100 drops in a single flush window

    public MarketFeedWorker(
        IMarketDataFeed feed,
        ILogger<MarketFeedWorker> logger,
        IServiceScopeFactory scopeFactory,
        ITradingAlertService alerts, IInstrumentCache instrumentCache,
        IOptions<MarketFeedSettings> marketFeedSettings)
    {
        _feed = feed;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _alerts = alerts;
        _instrumentCache = instrumentCache;

        var channelSize = marketFeedSettings.Value.ChannelSize;
        _tickChannel = Channel.CreateBounded<MarketTick>(new BoundedChannelOptions(channelSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true,
        });

        _feed.TickReceived += OnTickReceived;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!MarketHours.IsOpen())
                {
                    _logger.LogInformation(
                        "Market closed. Waiting... Current IST: {Time}",
                        IstClock.Now);

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var instruments = _instrumentCache.GetActive();

                if (instruments.Count == 0)
                {
                    _logger.LogWarning(
                        "MarketFeedWorker: no active instruments.");

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                _logger.LogInformation(
                    "MarketFeedWorker starting feed for {Count} instruments",
                    instruments.Count);

                var batchTask = BatchFlushLoopAsync(stoppingToken);

                await _feed.StartAsync(
                    instruments.ToList(),
                    stoppingToken);

                _tickChannel.Writer.TryComplete();

                await batchTask;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "MarketFeedWorker crashed. Restarting in 30 seconds...");

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            finally
            {
                try
                {
                    await _feed.StopAsync();
                }
                catch
                {
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _feed.TickReceived -= OnTickReceived;
        await _feed.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private Task OnTickReceived(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ltp", out var ltpEl)) return Task.CompletedTask;
            if (!decimal.TryParse(ltpEl.ToString(), out var ltp)) return Task.CompletedTask;

            var tokenStr = root.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "";
            var instruments = _instrumentCache.GetActive();
            var symbol = instruments.FirstOrDefault(s => s.Symbol == tokenStr).Symbol ?? tokenStr;
            if (string.IsNullOrEmpty(symbol)) return Task.CompletedTask;

            decimal open = ltp, high = ltp, low = ltp, close = ltp;
            long volume = 0;

            if (root.TryGetProperty("open", out var oEl) && decimal.TryParse(oEl.ToString(), out var o) && o > 0) open = o;
            if (root.TryGetProperty("high", out var hEl) && decimal.TryParse(hEl.ToString(), out var h) && h > 0) high = h;
            if (root.TryGetProperty("low", out var lEl) && decimal.TryParse(lEl.ToString(), out var l) && l > 0) low = l;
            if (root.TryGetProperty("close", out var cEl) && decimal.TryParse(cEl.ToString(), out var c) && c > 0) close = c;
            if (root.TryGetProperty("volume", out var vEl) && long.TryParse(vEl.ToString(), out var v)) volume = v;

            DateTime tickTime = IstClock.Now;
            if (root.TryGetProperty("exchangeTime", out var tsEl)
                && tsEl.TryGetDateTime(out var exTs))
                tickTime = exTs;

            var tick = new MarketTick
            {
                Symbol = symbol,
                LastPrice = ltp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                TickTime = tickTime
            };

            if (!_tickChannel.Writer.TryWrite(tick))
                Interlocked.Increment(ref _droppedTicks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tick parse error");
        }

        return Task.CompletedTask;
    }

    private async Task BatchFlushLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested || _tickChannel.Reader.Count > 0)
        {
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { }

            var dropped = Interlocked.Exchange(ref _droppedTicks, 0);
            if (dropped > 0)
            {
                var total = Interlocked.Add(ref _droppedTicksTotal, dropped);
                _logger.LogWarning("MarketFeedWorker — {Count} ticks dropped (channel full, total today: {Total})", dropped, total);

                if (dropped >= DropAlertThreshold)
                    await _alerts.SendCriticalAsync(
                        $"MarketFeedWorker: {dropped} ticks dropped in last second (channel full). Total today: {total}. Consider raising channel size.");
            }

            while (_tickChannel.Reader.Count > 0)
            {
                var batch = new List<MarketTick>(MaxBatchSize);
                while (batch.Count < MaxBatchSize && _tickChannel.Reader.TryRead(out var tick))
                    batch.Add(tick);

                if (batch.Count == 0) break;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<MarketTick>>();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    await repo.StageRangeAsync(batch);
                    await uow.SaveChangesAsync();

                    _logger.LogDebug("Saved {Count} ticks to DB", batch.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tick batch save error — {Count} ticks lost", batch.Count);
                    await _alerts.SendCriticalAsync($"MarketFeedWorker: tick batch save failed — {batch.Count} ticks lost\n{ex.Message}");
                }
            }
        }
    }
}
