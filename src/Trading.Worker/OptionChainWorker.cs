using Trading.Domain;
using System.Globalization;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;

namespace Trading.Worker;

public class OptionChainWorker : BackgroundService
{
    private readonly ILogger<OptionChainWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentCache _instrumentCache;

    public OptionChainWorker(
        ILogger<OptionChainWorker> logger,
        IServiceScopeFactory scopeFactory,
        IInstrumentCache instrumentCache,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _instrumentCache = instrumentCache;
        _period = RefreshPeriod(configuration.GetValue<int?>("OptionChain:RefreshSeconds"));
    }

    // Start-to-start period, from OptionChain:RefreshSeconds (default 15, clamped 5-60). The old
    // loop slept 30 s AFTER fetching, so a cycle took ~50 s; one cycle itself takes a median 10 s
    // (p90 17.5 s, 2026-10-05: three symbols fetched in sequence), so 15 s is the practical floor.
    internal const int DefaultRefreshSeconds = 15;
    private static readonly TimeSpan MinPause = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _period;

    /// <summary>Configured refresh period: default 15 s, clamped to 5-60 s. Exposed for testing.</summary>
    internal static TimeSpan RefreshPeriod(int? configuredSeconds)
        => TimeSpan.FromSeconds(Math.Clamp(configuredSeconds ?? DefaultRefreshSeconds, 5, 60));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!MarketHours.IsOpen())
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            var cycleStart = DateTime.UtcNow;
            var instruments = _instrumentCache.GetActive();

            if (instruments.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                continue;
            }

            foreach (var instrument in instruments)
            {
                if (string.IsNullOrWhiteSpace(instrument.Expiry))
                {
                    continue;
                }

                try
                {
                    await FetchAndPersistAsync(instrument, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Option chain fetch failed for {Symbol}", instrument.Symbol);
                }
            }

            await Task.Delay(NextDelay(_period, DateTime.UtcNow - cycleStart), stoppingToken);
        }
    }

    /// <summary>
    /// Pause so cycles start every <paramref name="period"/>; at least MinPause when a cycle overran,
    /// so a slow broker never turns this into a tight loop. Exposed for testing.
    /// </summary>
    internal static TimeSpan NextDelay(TimeSpan period, TimeSpan elapsed)
        => elapsed >= period ? MinPause : period - elapsed;

    /// <summary>Total PE OI / total CE OI over every strike; null when there is no CE OI. Exposed for testing.</summary>
    internal static decimal? CalculateFullChainPcr(IEnumerable<(long? CeOi, long? PeOi)> strikes)
    {
        long ce = 0, pe = 0;
        foreach (var (ceOi, peOi) in strikes)
        {
            ce += ceOi ?? 0L;
            pe += peOi ?? 0L;
        }
        return ce > 0 ? Math.Round((decimal)pe / ce, 4, MidpointRounding.AwayFromZero) : null;
    }

    private async Task FetchAndPersistAsync(TradingInstrument instrument, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var liveData = scope.ServiceProvider.GetRequiredService<IGrowwLiveDataService>();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<OptionChainData>>();
        var instrumentRepo = scope.ServiceProvider.GetRequiredService<IRepository<TradingInstrument>>();
        var indicatorRepo = scope.ServiceProvider.GetRequiredService<IRepository<SymbolIndicator>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var expiry = instrument.Expiry!;
        var dbSymbol = instrument.Symbol;
        var exchange = instrument.Exchange;
        var now = IstClock.Now;
        if (!DateTime.TryParseExact(expiry, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiryDate)
            || expiryDate.Date < now.Date)
        {
            var stale = await instrumentRepo.FirstOrDefaultAsync(i => i.Symbol == dbSymbol);
            if (stale is not null)
            {
                stale.Expiry = null;
                stale.UpdatedOn = now;
                await uow.SaveChangesAsync();
            }
            return;
        }

        var payload = await liveData.GetOptionChainAsync(exchange, dbSymbol, expiry);
        if (payload is null || payload.Strikes.Count == 0)
        {
            return;
        }

        var fullChainPcr = CalculateFullChainPcr(payload.Strikes.Values.Select(x => (x.CE?.OpenInterest, x.PE?.OpenInterest)));

        // Find ATM by closest strike to current spot
        var sortedStrikes = payload.Strikes.Keys
            .Select(k => decimal.TryParse(k, out var v) ? v : (decimal?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToList();

        var atmIndex = sortedStrikes
            .Select((s, i) => (s, i))
            .OrderBy(x => Math.Abs(x.s - (payload.UnderlyingLtp ?? 0m)))
            .First().i;

        // Gap 4 — widen to ATM±5 (11 strikes) for support/resistance analysis
        var from = Math.Max(0, atmIndex - 5);
        var to = Math.Min(sortedStrikes.Count - 1, atmIndex + 5);
        var nearStrikes = sortedStrikes.GetRange(from, to - from + 1).ToHashSet();

        var filteredStrikes = payload.Strikes
            .Where(kv => decimal.TryParse(kv.Key, out var s) && nearStrikes.Contains(s))
            .ToList();

        // Gap 1 + Gap 6 — load previous snapshot ONCE into a dict (O(1) lookup in loop, not N+1)
        var prevCutoff = now.AddMinutes(-5);
        var previousRows = await repo.FindAsync(
            o => o.Symbol == dbSymbol && o.CreatedOn >= prevCutoff);
        var previousOi = previousRows
            .GroupBy(o => o.TradingSymbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedOn).First().OpenInterest);

        var snapshotId = Guid.NewGuid().ToString();
        var rows = new List<OptionChainData>();

        foreach (var (strikeKey, entry) in filteredStrikes)
        {
            if (!decimal.TryParse(strikeKey, out var strikePrice))
                continue;

            foreach (var (optionType, leg) in new[] { (TradingConst.OptionType.CE, entry.CE), (TradingConst.OptionType.PE, entry.PE) })
            {
                if (leg is null) continue;

                // Gap 1 — calculate ChangeInOi from previous snapshot
                var changeInOi = previousOi.TryGetValue(leg.TradingSymbol, out var prevOi)
                    ? (leg.OpenInterest ?? 0) - prevOi
                    : 0L;

                var g = leg.Greeks;
                rows.Add(new OptionChainData
                {
                    Symbol = dbSymbol,
                    TradingSymbol = leg.TradingSymbol,
                    StrikePrice = strikePrice,
                    OptionType = optionType,
                    Expiry = expiry,
                    ExpiryDate = expiryDate,
                    Ltp = leg.Ltp ?? 0m,
                    OpenInterest = leg.OpenInterest ?? 0,
                    ChangeInOi = changeInOi,
                    Volume = leg.Volume ?? 0,
                    ImpliedVolatility = g?.Iv ?? 0,
                    Delta = g?.Delta ?? 0,
                    Gamma = g?.Gamma ?? 0,
                    Theta = g?.Theta ?? 0,
                    Vega = g?.Vega ?? 0,
                    Rho = g?.Rho ?? 0,
                    // Gap 2 — store underlying spot price on every row
                    UnderlyingLtp = payload.UnderlyingLtp ?? 0m,
                    SnapshotId = snapshotId,
                    CreatedOn = now
                });
            }
        }

        if (rows.Count == 0)
        {
            return;
        }

        var totalPeOi = rows.Where(r => r.OptionType == TradingConst.OptionType.PE).Sum(r => r.OpenInterest);
        var totalCeOi = rows.Where(r => r.OptionType == TradingConst.OptionType.CE).Sum(r => r.OpenInterest);
        var pcr = totalCeOi == 0 ? 0m : Math.Round((decimal)totalPeOi / totalCeOi, 4);

        var pcrIndicator = await indicatorRepo.FirstOrDefaultAsync(
            i => i.Symbol == dbSymbol && i.IndicatorName == TradingConst.Indicator.PcrAtm);

        if (pcrIndicator is null)
        {
            await indicatorRepo.StageAsync(new SymbolIndicator
            {
                Symbol = dbSymbol,
                IndicatorName = TradingConst.Indicator.PcrAtm,
                Value = pcr,
                UpdatedOn = now
            });
        }
        else
        {
            pcrIndicator.Value = pcr;
            pcrIndicator.UpdatedOn = now;
        }

        // Full-chain PCR (all strikes) is read by MarketSnapshot. Null (no CE OI) is not written,
        // so the previous row goes stale and the reader treats it as missing.
        if (fullChainPcr.HasValue)
        {
            var fullIndicator = await indicatorRepo.FirstOrDefaultAsync(
                i => i.Symbol == dbSymbol && i.IndicatorName == TradingConst.Indicator.PcrFullChain);

            if (fullIndicator is null)
            {
                await indicatorRepo.StageAsync(new SymbolIndicator
                {
                    Symbol = dbSymbol,
                    IndicatorName = TradingConst.Indicator.PcrFullChain,
                    Value = fullChainPcr.Value,
                    UpdatedOn = now
                });
            }
            else
            {
                fullIndicator.Value = fullChainPcr.Value;
                fullIndicator.UpdatedOn = now;
            }
        }

        await repo.StageRangeAsync(rows);
        await uow.SaveChangesAsync();

        // Per-minute history for backtests. After the chain save and before the prune, so the
        // live chain is already persisted whatever happens here, and best-effort so a failure
        // can never stop the chain feed the exit path depends on.
        await SaveMinuteHistoryAsync(scope.ServiceProvider, uow, dbSymbol, rows, now, ct);

        // Prune snapshots older than 2 hours
        var pruneCutoff = now.AddHours(-2);
        await repo.ExecuteDeleteAsync(x => x.Symbol == dbSymbol && x.CreatedOn < pruneCutoff);
    }

    /// <summary>
    /// First chain fetch of each minute is copied to OptionMinuteHistory (one row per leg).
    /// Idempotent: skipped when that minute is already stored; the unique index is the backstop.
    /// </summary>
    private async Task SaveMinuteHistoryAsync(
        IServiceProvider sp, IUnitOfWork uow, string symbol, List<OptionChainData> rows, DateTime now, CancellationToken ct)
    {
        try
        {
            var minute = ToMinute(now);
            var historyRepo = sp.GetRequiredService<IRepository<OptionMinuteHistory>>();

            var already = await historyRepo.FirstOrDefaultAsync(h => h.Symbol == symbol && h.MinuteTime == minute);
            if (already is not null) return;

            await historyRepo.StageRangeAsync(ToMinuteHistory(rows, minute, now));
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OptionMinuteHistory write skipped for {Symbol} at {Now}", symbol, now);
        }
    }

    internal static DateTime ToMinute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind);

    /// <summary>Pure mapping from a chain snapshot to its minute-history rows. Exposed for testing.</summary>
    internal static List<OptionMinuteHistory> ToMinuteHistory(IEnumerable<OptionChainData> rows, DateTime minute, DateTime now)
        => rows
            .Where(r => r.Ltp > 0 && !string.IsNullOrWhiteSpace(r.TradingSymbol))
            .GroupBy(r => r.TradingSymbol)
            .Select(g => g.First())
            .Select(r => new OptionMinuteHistory
            {
                Symbol = r.Symbol,
                MinuteTime = minute,
                TradingSymbol = r.TradingSymbol,
                StrikePrice = r.StrikePrice,
                OptionType = r.OptionType,
                ExpiryDate = r.ExpiryDate,
                Ltp = r.Ltp,
                Delta = r.Delta,
                ImpliedVolatility = r.ImpliedVolatility,
                OpenInterest = r.OpenInterest,
                ChangeInOi = r.ChangeInOi,
                UnderlyingLtp = r.UnderlyingLtp,
                CreatedOn = now,
            })
            .ToList();
}
