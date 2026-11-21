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
            // Wake a few seconds after each minute closes, not 60 s after the previous run: the old
            // free-running delay had a random phase, so a closed 1m candle waited 0-60 s (median
            // ~30 s, 2026-10-01/05) before the signal snapshot could see it.
            var offset = TimeSpan.FromSeconds(Math.Clamp(_settings.Value.MinuteOffsetSeconds, 0, 30));
            try { await Task.Delay(DelayUntilNextMinute(DateTime.UtcNow, offset), stoppingToken); }
            catch (OperationCanceledException) { break; }

            if (!MarketHours.IsOpen())
                continue;

            await RunAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Time until the next minute boundary + offset (minute boundaries are the same in UTC and IST).
    /// If that moment has already passed this minute, the next one. Exposed for testing.
    /// </summary>
    internal static TimeSpan DelayUntilNextMinute(DateTime nowUtc, TimeSpan offset)
    {
        var target = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMinute, nowUtc.Kind) + offset;
        if (target <= nowUtc) target = target.AddMinutes(1);
        return target - nowUtc;
    }

    /// <summary>
    /// Start of the refresh window, in IST wall clock (what Groww's start_time and sessionOpen use). Candle.CandleTime is
    /// UTC, so it is shifted to IST first (India has no DST); comparing it to sessionOpen unshifted put the window before
    /// session open on every cycle, so the whole day was re-downloaded each minute (seen 2026-10-12: start_time=09:15 at
    /// 13:57). Never earlier than session open. Exposed for testing.
    /// </summary>
    internal static DateTime RefreshFromIst(DateTime lastCandleUtc, int refreshMinutes, DateTime sessionOpenIst)
    {
        var from = lastCandleUtc.AddHours(5).AddMinutes(30).AddMinutes(-refreshMinutes);
        return from < sessionOpenIst ? sessionOpenIst : from;
    }

    /// <summary>
    /// Whether the futures sync may switch to the next contract: the near-month one returned nothing, the session is
    /// past its first 3 minutes, and the series has no bars today (otherwise the next contract would mix into it).
    /// </summary>
    internal static bool ShouldTryFallbackContract(int nearMonthCount, DateTime nowIst, DateTime sessionOpenIst, bool hasBarsToday)
        => nearMonthCount == 0 && nowIst >= sessionOpenIst.AddMinutes(3) && !hasBarsToday;

    internal async Task RunAsync(CancellationToken ct)
    {
        var instruments = _instrumentCache.GetActive();
        if (instruments.Count == 0)
        {
            return;
        }

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

        //if (_settings.Value.FetchIndexFutures && nowIst >= sessionOpen)
        //    await SyncFuturesAsync(instrument, candleRepo, historicalSvc, uow, sessionOpen, nowIst, ct);
    }

    /// <summary>
    /// The near-month index future's 1m candles (FNO), stored as {SYMBOL}_FUT_1m - the volume source for VWAP and
    /// volume (the index publishes none). Never blocks or fails the index candles above: errors are logged and the
    /// cycle carries on. A near-month contract that has already expired (an expiry earlier than the roll window)
    /// returns nothing, so the next contract is tried.
    /// </summary>
    private async Task SyncFuturesAsync(
        TradingInstrument instrument,
        IRepository<Candle> repo,
        IHistoricalDataService historicalSvc,
        IUnitOfWork uow,
        DateTime sessionOpen,
        DateTime nowIst,
        CancellationToken ct)
    {
        var roll = _settings.Value.FuturesRollDaysBeforeMonthEnd;
        var candleSymbol = GrowwFutures.CandleSymbol(instrument.Symbol);
        var contract = GrowwFutures.NearMonth(instrument.Symbol, nowIst, roll);

        int count;
        try
        {
            count = await SyncInterval(instrument, GrowwInterval.OneMinute, candleSymbol, repo, historicalSvc, uow,
                sessionOpen, nowIst, refreshMinutes: 5, ct, contract, GrowwSegment.Fno);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HistoricalDataWorker: futures candles failed for {Contract}", contract);
            count = 0;
        }

        // A thin contract (SENSEX) can return nothing for one refresh window. Falling back then would write the NEXT
        // contract's bars - a different running volume and basis - into the same series, and the falling volume makes
        // FuturesFlow drop the day's VWAP (SENSEX 2026-10-26 11:49). Fall back only while the day has no bars at all.
        bool hasBarsToday;
        try
        {
            hasBarsToday = await repo.FirstOrDefaultAsync(c => c.Symbol == candleSymbol && c.CandleTime >= sessionOpen.AddMinutes(-330)) != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HistoricalDataWorker: could not check today's {Series} bars - skipping the fallback contract", candleSymbol);
            return;
        }

        if (!ShouldTryFallbackContract(count, nowIst, sessionOpen, hasBarsToday))
            return;

        //var next = GrowwFutures.NextAfterNearMonth(instrument.Symbol, nowIst, roll);
        //try
        //{
        //    var nextCount = await SyncInterval(instrument, GrowwInterval.OneMinute, candleSymbol, repo, historicalSvc, uow,
        //        sessionOpen, nowIst, refreshMinutes: 5, ct, next, GrowwSegment.Fno);
        //    _logger.LogWarning("HistoricalDataWorker: no candles for {Contract}; {Next} returned {Count}", contract, next, nextCount);
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogWarning(ex, "HistoricalDataWorker: futures candles failed for {Contract} and {Next} - no volume/VWAP this cycle",
        //        contract, next);
        //}
    }

    /// <summary>Fetches and upserts one candle series. Returns how many candles Groww returned.</summary>
    private async Task<int> SyncInterval(
    TradingInstrument instrument,
    string interval,
    string candleSymbol,
    IRepository<Candle> repo,
    IHistoricalDataService historicalSvc,
    IUnitOfWork uow,
    DateTime sessionOpen,
    DateTime nowIst,
    int refreshMinutes,
    CancellationToken ct,
    string? tradingSymbol = null,
    string segment = GrowwSegment.Cash)
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
            fromIst = RefreshFromIst(last.CandleTime, refreshMinutes, sessionOpen);
        }

        var candles = await historicalSvc.GetCandlesAsync(
            instrument.Exchange,
            tradingSymbol ?? instrument.Symbol,
            interval,
            fromIst,
            nowIst,
            segment);

        if (candles.Count == 0)
            return 0;

        await UpsertCandlesAsync(
            repo,
            uow,
            candleSymbol,
            candles,
            ct);

        return candles.Count;
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
