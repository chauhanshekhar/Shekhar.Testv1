using Trading.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.Worker;

/// <summary>
/// Calculates EMA20 / EMA50 every 10 seconds during market hours
/// and persists the latest values to SymbolIndicators.
///
/// Architecture note (Fix #1):
///   EmaWorker  → calculate + store indicators ONLY
///   SignalWorker → consume indicators + generate signals + place orders
/// </summary>
public class EmaWorker : BackgroundService
{
    private readonly ILogger<EmaWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentCache _instrumentCache;

    // Keep last 100 candles per symbol — enough for EMA50 with buffer
    private const int CandleCount = 100;

    public EmaWorker(ILogger<EmaWorker> logger, IServiceScopeFactory scopeFactory, IInstrumentCache instrumentCache)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _instrumentCache = instrumentCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!MarketHours.IsOpen())
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                foreach (var symbol in _instrumentCache.GetActive().Select(i => i.Symbol))
                {
                    try { await ProcessSymbol(symbol); }
                    catch (Exception ex)
                    { _logger.LogError(ex, "EmaWorker error for {Symbol}", symbol); }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmaWorker loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessSymbol(string symbol)
    {
        using var scope = _scopeFactory.CreateScope();
        var candleRepo = scope.ServiceProvider.GetRequiredService<IRepository<Candle>>();
        var indicatorRepo = scope.ServiceProvider.GetRequiredService<IRepository<SymbolIndicator>>();
        var emaService = scope.ServiceProvider.GetRequiredService<EmaService>();
        var uow = scope.ServiceProvider.GetRequiredService<Trading.Application.Interfaces.IUnitOfWork>();

        var dayStart = IstClock.TodayStart;
        var dayEnd = dayStart.AddDays(1);

        // EMA uses only _1m candles — using _5m as well would double-count periods.
        var sym1m = $"{symbol}_1m";

        var candles = await candleRepo.FindAsync(
            c => c.Symbol == sym1m
              && c.CandleTime >= dayStart
              && c.CandleTime < dayEnd,
            q => q.OrderByDescending(c => c.CandleTime).Take(CandleCount));

        // Reverse to get chronological order for EMA calculation
        candles = candles.OrderBy(c => c.CandleTime).ToList();

        // Early in the session there aren't enough same-day closes yet for EMA50 — pad the
        // front of the series with the tail of the previous trading session's candles so
        // EMA becomes available right after market open. EMA math itself is untouched.
        if (candles.Count < CandleCount)
        {
            var needed = CandleCount - candles.Count;
            var prevSessionStart = MarketHours.PreviousTradingSession(dayStart);
            var prevSessionEnd = prevSessionStart.AddDays(1);

            var prevCandles = await candleRepo.FindAsync(
                c => c.Symbol == sym1m
                  && c.CandleTime >= prevSessionStart
                  && c.CandleTime < prevSessionEnd,
                q => q.OrderByDescending(c => c.CandleTime).Take(needed));

            candles = prevCandles
                .OrderBy(c => c.CandleTime)
                .Concat(candles)
                .ToList();
        }

        if (candles.Count < 51)
        {
            _logger.LogDebug("EmaWorker {Symbol}: only {Count} candles including previous session — need 51", symbol, candles.Count);
            return;
        }

        var closes = candles.Select(c => c.Close).ToList();
        var ema20 = emaService.GetEma(closes, 20);
        var ema50 = emaService.GetEma(closes, 50);

        // Fix #1 — NO signal generation here. EmaWorker only stores indicator values.
        //           SignalWorker is solely responsible for generating signals.

        bool changed = false;
        // Use _1M suffix — consistent with VWAP_1M, leaves room for EMA20_5M etc. later.
        changed |= await UpsertIndicatorAsync(indicatorRepo, symbol, "EMA20_1M", ema20);
        changed |= await UpsertIndicatorAsync(indicatorRepo, symbol, "EMA50_1M", ema50);

        if (changed)
        {
            await uow.SaveChangesAsync();
            _logger.LogDebug("EMA {Symbol}: EMA20={E20:F2} EMA50={E50:F2}", symbol, ema20, ema50);
        }
    }

    /// <summary>
    /// Upserts one SymbolIndicator row.
    /// Fix #3 — uses FirstOrDefaultAsync (single SQL lookup, not GetAllAsync).
    /// Fix #6 — only marks the row dirty when the value actually changed.
    /// Returns true when a DB write is needed.
    /// </summary>
    private static async Task<bool> UpsertIndicatorAsync(
        IRepository<SymbolIndicator> repo,
        string symbol,
        string name,
        decimal value)
    {
        // Fix #3 — targeted lookup instead of loading the whole table
        var existing = await repo.FirstOrDefaultAsync(
            i => i.Symbol == symbol && i.IndicatorName == name);

        if (existing is not null)
        {
            // Fix #6 — skip update if value hasn't changed
            if (existing.Value == value) return false;

            existing.Value = value;
            existing.UpdatedOn = IstClock.Now;
            return true;   // UoW will persist on next SaveChanges
        }

        await repo.AddAsync(new SymbolIndicator
        {
            Symbol = symbol,
            IndicatorName = name,
            Value = value,
            UpdatedOn = IstClock.Now
        });

        return true;
    }

}
