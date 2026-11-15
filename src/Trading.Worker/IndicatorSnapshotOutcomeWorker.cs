using Microsoft.EntityFrameworkCore;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Data;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Analytics;

namespace Trading.Worker;

/// <summary>
/// Calculates, for every valid Bullish/Bearish IndicatorSnapshot, whether the INDEX reached
/// +20 points in the snapshot's direction (TARGET) or -20 points against it (SL) first, within
/// 5, 10 and 15 minutes.
///
/// ANALYTICS ONLY. Runs as its own BackgroundService in its own DI scope, so it can never delay
/// signal generation, strategy selection, or broker order placement. It reads IndicatorSnapshots
/// and writes ONLY IndicatorSnapshotOutcomes — it never modifies a snapshot, a Signal, or an
/// order, and it deletes nothing.
///
/// Historical backfill and live tracking are the SAME code path: "live" is just a snapshot whose
/// window has only recently elapsed. A snapshot is picked up once its 5-minute window closes,
/// filled in as far as elapsed time allows, and revisited until all three windows are done — so a
/// recent snapshot is never prematurely recorded as NONE.
///
/// Off by default (TradingSettings.EnableOutcomeTracking). Deploying this code starts no work.
/// </summary>
public class IndicatorSnapshotOutcomeWorker : BackgroundService
{
    private const int FinalWindowMinutes = 15;
    private const int FirstWindowMinutes = 5;

    private readonly ILogger<IndicatorSnapshotOutcomeWorker> _logger;
    private readonly IDbContextFactory<TradingDbContext> _dbFactory;
    private readonly SystemSettingsService _settingsService;
    private readonly IStrategySettingsService _strategySettings;
    private readonly AppErrorLogger _errorLogger;

    public IndicatorSnapshotOutcomeWorker(
        ILogger<IndicatorSnapshotOutcomeWorker> logger,
        IDbContextFactory<TradingDbContext> dbFactory,
        SystemSettingsService settingsService,
        IStrategySettingsService strategySettings,
        AppErrorLogger errorLogger)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _settingsService = settingsService;
        _strategySettings = strategySettings;
        _errorLogger = errorLogger;
    }

    /// <summary>
    /// DB-backed kill switch, falling back to the appsettings value when no row exists.
    ///
    /// TradingSettings is deliberately appsettings-only so TRADING behaviour can't be taken down
    /// by a database outage. That guarantee doesn't apply here: this worker is analytics, and if
    /// the DB is unreachable there is nothing for it to read or write anyway. Reading the flag
    /// from StrategySettings instead means it can be switched off mid-session with one UPDATE
    /// (picked up within the cache's 5-minute refresh) rather than needing a container rebuild —
    /// which matters because the one scenario that warrants killing it, a backfill still draining
    /// at market open, is exactly when a restart is least welcome.
    /// </summary>
    private bool IsEnabled(TradingSettings cfg)
        => _strategySettings.GetBool("EnableOutcomeTracking", null, cfg.EnableOutcomeTracking);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup settle before touching the database.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = 60;

            try
            {
                var cfg = await _settingsService.GetAsync(stoppingToken);
                intervalSeconds = Math.Max(10, cfg.OutcomeTrackingIntervalSeconds);

                if (IsEnabled(cfg))
                {
                    var processed = await RunPassAsync(cfg, stoppingToken);

                    // A full batch means a backlog is still draining — come back promptly instead
                    // of waiting a full interval.
                    if (processed >= cfg.OutcomeTrackingBatchSize)
                        intervalSeconds = 1;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Analytics must never take the worker host down, but the failure is recorded.
                _logger.LogError(ex, "IndicatorSnapshotOutcomeWorker pass failed");
                await _errorLogger.LogAsync("IndicatorSnapshotOutcomeWorker", ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Returns how many snapshots were processed in this pass.</summary>
    private async Task<int> RunPassAsync(Trading.Infrastructure.TradingSettings cfg, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var now = IstClock.Now;
        var batchSize = Math.Max(1, cfg.OutcomeTrackingBatchSize);
        var backfillCutoff = now.AddDays(-Math.Max(1, cfg.OutcomeTrackingBackfillDays));

        // Only snapshots whose FIRST window has closed are eligible — nothing is classified before
        // its window has actually elapsed.
        var eligibleBefore = now.AddMinutes(-FirstWindowMinutes);

        // ── Work item 1: snapshots that have no outcome row yet ──────────────────────────────
        // Anti-join rather than a client-side filter, so the database does the exclusion.
        var newWork = await db.IndicatorSnapshots
            .Where(s => s.SnapshotTime >= backfillCutoff
                     && s.SnapshotTime <= eligibleBefore
                     && (s.TrendDirection == "Bullish" || s.TrendDirection == "Bearish")
                     && !db.IndicatorSnapshotOutcomes.Any(o => o.IndicatorSnapshotId == s.Id))
            .OrderBy(s => s.SnapshotTime)
            .Take(batchSize)
            .Select(s => new SnapshotWork(s.Id, s.Symbol, s.TrendDirection, s.Ltp, s.SnapshotTime))
            .ToListAsync(ct);

        // ── Work item 2: existing rows whose next window has since elapsed ───────────────────
        var remaining = batchSize - newWork.Count;
        var pendingIds = new List<long>();

        if (remaining > 0)
        {
            pendingIds = await db.IndicatorSnapshotOutcomes
                .Where(o => o.CompletedWindowMinutes < FinalWindowMinutes && o.Anomaly == null)
                .OrderBy(o => o.Id)
                .Take(remaining)
                .Select(o => o.IndicatorSnapshotId)
                .ToListAsync(ct);
        }

        if (newWork.Count == 0 && pendingIds.Count == 0)
            return 0;

        var pendingWork = pendingIds.Count == 0
            ? new List<SnapshotWork>()
            : await db.IndicatorSnapshots
                .Where(s => pendingIds.Contains(s.Id))
                .Select(s => new SnapshotWork(s.Id, s.Symbol, s.TrendDirection, s.Ltp, s.SnapshotTime))
                .ToListAsync(ct);

        var work = newWork.Concat(pendingWork).ToList();

        // ── Future prices: ONE query per symbol for the whole batch, not one per snapshot ────
        // Bounded to [earliest entry, latest entry + 15m] so it can never degrade into a
        // full-table scan, and served by the existing IX_IndicatorSnapshot_Symbol_SnapshotTime.
        var futureBySymbol = await LoadFuturePricesAsync(db, work, ct);

        var existing = await db.IndicatorSnapshotOutcomes
            .Where(o => pendingIds.Contains(o.IndicatorSnapshotId))
            .ToDictionaryAsync(o => o.IndicatorSnapshotId, ct);

        var inserts = new List<IndicatorSnapshotOutcome>();

        foreach (var item in work)
        {
            var direction = OutcomeBarrierCalculator.DirectionFromTrend(item.TrendDirection);
            if (direction == null)
            {
                // Should be impossible — the query filters to Bullish/Bearish — so reaching here
                // means unexpected data, which is logged rather than assumed away.
                _logger.LogWarning(
                    "Skipping snapshot with unexpected TrendDirection. SnapshotId={SnapshotId} TrendDirection={TrendDirection}",
                    item.Id, item.TrendDirection);
                continue;
            }

            if (item.Ltp <= 0)
            {
                _logger.LogWarning(
                    "Skipping snapshot with no usable entry price. SnapshotId={SnapshotId} Symbol={Symbol} Ltp={Ltp}",
                    item.Id, item.Symbol, item.Ltp);
                continue;
            }

            // Same symbol only — a BANKNIFTY entry can never be scored against NIFTY prices.
            if (!futureBySymbol.TryGetValue(item.Symbol, out var series))
                series = [];

            var slice = SliceForward(series, item.SnapshotTime, FinalWindowMinutes);

            var calc = OutcomeBarrierCalculator.Calculate(
                direction, item.Ltp, item.SnapshotTime, slice, now);

            if (calc.Anomaly != null)
            {
                _logger.LogError(
                    "IndicatorSnapshot outcome anomaly. SnapshotId={SnapshotId} Symbol={Symbol} Anomaly={Anomaly}",
                    item.Id, item.Symbol, calc.Anomaly);
            }

            if (existing.TryGetValue(item.Id, out var row))
            {
                Apply(row, calc, now);
            }
            else
            {
                var created = new IndicatorSnapshotOutcome
                {
                    IndicatorSnapshotId = item.Id,
                    Symbol = item.Symbol,
                    Direction = direction,
                    EntryPrice = item.Ltp,
                    TargetPoints = OutcomeBarrierCalculator.BarrierPoints,
                    StopLossPoints = OutcomeBarrierCalculator.BarrierPoints,
                    CreatedOn = now,
                };
                Apply(created, calc, now);
                inserts.Add(created);
            }
        }

        if (inserts.Count > 0)
            await db.IndicatorSnapshotOutcomes.AddRangeAsync(inserts, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Most likely the unique index on IndicatorSnapshotId rejecting a concurrent second
            // writer. The batch is abandoned and retried next pass; nothing is duplicated and
            // nothing is silently dropped.
            _logger.LogError(ex,
                "Failed to persist IndicatorSnapshot outcomes. Inserts={Inserts} Updates={Updates}",
                inserts.Count, existing.Count);
            await _errorLogger.LogAsync("IndicatorSnapshotOutcomeWorker.Save", ex);
            return 0;
        }

        _logger.LogInformation(
            "IndicatorSnapshot outcomes calculated. New={New} Updated={Updated} Symbols={Symbols}",
            inserts.Count, existing.Count, futureBySymbol.Count);

        return work.Count;
    }

    private static void Apply(IndicatorSnapshotOutcome row, OutcomeCalculation calc, DateTime now)
    {
        if (calc.Window5.Result != null)
        {
            row.FirstHitResult5Min = calc.Window5.Result;
            row.Target20Hit5Min = calc.Window5.TargetHit;
            row.SL20Hit5Min = calc.Window5.SlHit;
            row.MaxFavorableMove5 = calc.Window5.MaxFavorable;
            row.MaxAdverseMove5 = calc.Window5.MaxAdverse;
        }

        if (calc.Window10.Result != null)
        {
            row.FirstHitResult10Min = calc.Window10.Result;
            row.Target20Hit10Min = calc.Window10.TargetHit;
            row.SL20Hit10Min = calc.Window10.SlHit;
            row.MaxFavorableMove10 = calc.Window10.MaxFavorable;
            row.MaxAdverseMove10 = calc.Window10.MaxAdverse;
        }

        if (calc.Window15.Result != null)
        {
            row.FirstHitResult15Min = calc.Window15.Result;
            row.Target20Hit15Min = calc.Window15.TargetHit;
            row.SL20Hit15Min = calc.Window15.SlHit;
            row.MaxFavorableMove15 = calc.Window15.MaxFavorable;
            row.MaxAdverseMove15 = calc.Window15.MaxAdverse;
        }

        if (calc.SecondsToFirstHit.HasValue)
            row.SecondsToFirstHit = calc.SecondsToFirstHit;

        row.CompletedWindowMinutes = Math.Max(row.CompletedWindowMinutes, calc.CompletedWindowMinutes);
        row.Anomaly = calc.Anomaly ?? row.Anomaly;
        row.CalculatedOn = now;

        // An anomalous path is terminal — stop revisiting it every pass.
        if (row.Anomaly != null)
            row.CompletedWindowMinutes = FinalWindowMinutes;
    }

    /// <summary>
    /// One query per symbol covering the whole batch's time span. Projects to (time, price) only —
    /// the full snapshot rows are never materialised.
    /// </summary>
    private static async Task<Dictionary<string, FuturePoint[]>> LoadFuturePricesAsync(
        TradingDbContext db, List<SnapshotWork> work, CancellationToken ct)
    {
        var result = new Dictionary<string, FuturePoint[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in work.GroupBy(w => w.Symbol, StringComparer.OrdinalIgnoreCase))
        {
            var symbol = group.Key;
            var from = group.Min(w => w.SnapshotTime);
            var to = group.Max(w => w.SnapshotTime).AddMinutes(FinalWindowMinutes);

            var points = await db.IndicatorSnapshots
                .Where(s => s.Symbol == symbol
                         && s.SnapshotTime > from
                         && s.SnapshotTime <= to
                         && s.Ltp > 0)                 // no usable price -> not a future data point
                .OrderBy(s => s.SnapshotTime)
                .Select(s => new FuturePoint(s.SnapshotTime, s.Ltp))
                .ToArrayAsync(ct);

            result[symbol] = points;
        }

        return result;
    }

    /// <summary>
    /// Binary-searches the symbol's ordered series for the slice covering (entry, entry+minutes],
    /// so each snapshot touches only its own window instead of re-scanning the batch's series.
    /// </summary>
    private static ArraySegment<FuturePoint> SliceForward(FuturePoint[] series, DateTime entryTime, int minutes)
    {
        if (series.Length == 0)
            return ArraySegment<FuturePoint>.Empty;

        var start = LowerBound(series, entryTime);              // first index with Time > entryTime
        var end = LowerBound(series, entryTime.AddMinutes(minutes));  // first index beyond the window

        return start >= end
            ? ArraySegment<FuturePoint>.Empty
            : new ArraySegment<FuturePoint>(series, start, end - start);
    }

    /// <summary>First index whose Time is strictly greater than <paramref name="time"/>.</summary>
    private static int LowerBound(FuturePoint[] series, DateTime time)
    {
        int lo = 0, hi = series.Length;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (series[mid].Time <= time) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private readonly record struct SnapshotWork(
        long Id, string Symbol, string TrendDirection, decimal Ltp, DateTime SnapshotTime);
}
