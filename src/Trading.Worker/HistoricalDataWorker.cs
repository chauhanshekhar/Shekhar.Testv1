using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Repositories;

namespace Trading.Worker;

public class HistoricalDataWorker : BackgroundService
{
    private readonly ILogger<HistoricalDataWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentCache _instrumentCache;
    private readonly IOptions<HistoricalDataSettings> _settings;

    public HistoricalDataWorker(
        ILogger<HistoricalDataWorker> logger,
        IServiceScopeFactory scopeFactory,
        IInstrumentCache instrumentCache,
        IOptions<HistoricalDataSettings> settings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _instrumentCache = instrumentCache;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }

            if (!MarketHours.IsOpen())
                continue;

            await RunAsync(stoppingToken);
        }
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        var instruments = _instrumentCache.GetActive();
        if (instruments.Count == 0)
        {
            _logger.LogDebug("HistoricalDataWorker: no active instruments");
            return;
        }

        _logger.LogInformation(
            "HistoricalDataWorker: fetching candles for {Count} instrument(s)", instruments.Count);

        foreach (var instrument in instruments)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await FetchAndPersistAsync(instrument, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HistoricalDataWorker: error for {Symbol}", instrument.Symbol);
            }
        }
    }

    private async Task FetchAndPersistAsync(
        TradingInstrument instrument,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var historicalSvc = scope.ServiceProvider.GetRequiredService<IHistoricalDataService>();
        var candleRepo = scope.ServiceProvider.GetRequiredService<IRepository<Candle>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var nowIst = IstClock.Now;
        var sessionOpen = nowIst.Date.Add(MarketHours.SessionOpen);

        await SyncInterval(
            instrument,
            GrowwInterval.OneMinute,
            $"{instrument.Symbol}_1m",
            candleRepo,
            historicalSvc,
            uow,
            sessionOpen,
            nowIst,
            refreshMinutes: 5,
            ct);

        await SyncInterval(
            instrument,
            GrowwInterval.FiveMinute,
            $"{instrument.Symbol}_5m",
            candleRepo,
            historicalSvc,
            uow,
            sessionOpen,
            nowIst,
            refreshMinutes: 15,
            ct);
    }
    private async Task SyncInterval(
    TradingInstrument instrument,
    string interval,
    string candleSymbol,
    IRepository<Candle> repo,
    IHistoricalDataService historicalSvc,
    IUnitOfWork uow,
    DateTime sessionOpen,
    DateTime nowIst,
    int refreshMinutes,
    CancellationToken ct)
    {
        var last = (await repo.FindAsync(c => c.Symbol == candleSymbol))
            .OrderByDescending(x => x.CandleTime)
            .FirstOrDefault();

        DateTime fromIst;

        if (last == null)
        {
            // No candles at all for this symbol yet — extend the look-back by the configured
            // backfill window in addition to the session-open floor (never fetches less than
            // the previous sessionOpen-only behaviour, only ever extends it earlier).
            var backfillFloor = nowIst.AddMinutes(-_settings.Value.StartupBackfillMinutes);
            fromIst = backfillFloor < sessionOpen ? backfillFloor : sessionOpen;
        }
        else
        {
            fromIst = last.CandleTime.AddMinutes(-refreshMinutes);

            if (fromIst < sessionOpen)
                fromIst = sessionOpen;
        }

        var candles = await historicalSvc.GetCandlesAsync(
            instrument.Exchange,
            instrument.Symbol,
            interval,
            fromIst,
            nowIst,
            GrowwSegment.Cash);

        if (candles.Count == 0)
            return;

        await UpsertCandlesAsync(
            repo,
            uow,
            candleSymbol,
            candles,
            ct);

        _logger.LogInformation(
            "{Symbol} {Interval} synced {Count} candles ({From} -> {To})",
            instrument.Symbol,
            interval,
            candles.Count,
            fromIst,
            nowIst);
    }
    private async Task UpsertCandlesAsync(
     IRepository<Candle> repo,
     IUnitOfWork uow,
     string candleSymbol,
     List<Candle> incoming,
     CancellationToken ct)
    {
        if (incoming.Count == 0)
            return;

        var minTime = incoming.Min(x => x.CandleTime);
        var maxTime = incoming.Max(x => x.CandleTime);

        var existing = (await repo.FindAsync(
                c => c.Symbol == candleSymbol &&
                     c.CandleTime >= minTime &&
                     c.CandleTime <= maxTime))
            .ToDictionary(x => x.CandleTime);

        var inserts = new List<Candle>();

        foreach (var candle in incoming)
        {
            if (existing.TryGetValue(candle.CandleTime, out var db))
            {
                db.Open = candle.Open;
                db.High = candle.High;
                db.Low = candle.Low;
                db.Close = candle.Close;
                db.Volume = candle.Volume;
                db.OpenInterest = candle.OpenInterest;

                try
                {
                    await repo.UpdateAsync(db);
                }
                catch
                {
                    // Entity may already be tracked.
                    // Property changes will still be saved by SaveChanges().
                }
            }
            else
            {
                inserts.Add(new Candle
                {
                    Symbol = candleSymbol,
                    Open = candle.Open,
                    High = candle.High,
                    Low = candle.Low,
                    Close = candle.Close,
                    Volume = candle.Volume,
                    OpenInterest = candle.OpenInterest,
                    CandleTime = candle.CandleTime
                });
            }
        }

        if (inserts.Count > 0)
            await repo.StageRangeAsync(inserts);

        await uow.SaveChangesAsync(ct);
    }
}
