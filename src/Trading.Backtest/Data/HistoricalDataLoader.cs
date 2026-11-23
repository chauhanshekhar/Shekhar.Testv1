using Microsoft.EntityFrameworkCore;
using Trading.Backtest.Reporting;
using Trading.Backtest.Settings;
using Trading.Domain.Entities;
using Trading.Infrastructure.Data;

namespace Trading.Backtest.Data;

/// <summary>
/// Read-only access to the history the replay needs. Every query is a projection with AsNoTracking (or a SELECT), and the
/// context is never saved, so a run cannot change the database. Candle times are UTC; snapshot and option times are IST.
/// </summary>
public sealed class HistoricalDataLoader
{
    private readonly DbContextOptions<TradingDbContext> _options;

    public DbContextOptions<TradingDbContext> Options => _options;

    public HistoricalDataLoader(string connectionString)
    {
        _options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
    }


          public async Task<List<Bar>> LoadBarsAsync(
    string series,
    DateTime fromUtc,
    CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);

        var historyFromUtc = fromUtc.AddDays(-10);

        var rows = await db.Candles
            .Where(c => c.Symbol == series &&
                        c.CandleTime >= historyFromUtc)
            .OrderBy(c => c.CandleTime)
            .Select(c => new
            {
                c.CandleTime,
                c.Open,
                c.High,
                c.Low,
                c.Close
            })
            .ToListAsync(ct);

        return rows.Select(c => new Bar(
            c.CandleTime,
            (double)c.Open,
            (double)c.High,
            (double)c.Low,
            (double)c.Close
        )).ToList();
    
    }

    /// <summary>One row per symbol-minute: the latest snapshot second of that minute (PCR, MarketState, trend, selector).</summary>
    public async Task<List<SnapshotRow>> LoadSnapshotsAsync(DateTime fromIst, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return await db.Database.SqlQuery<SnapshotRow>($@"
            WITH x AS (
                SELECT Symbol, SnapshotTime, Pcr, MarketState, TrendDirection, StrategySelected, MarketRegime,
                       ROW_NUMBER() OVER (PARTITION BY Symbol, DATEADD(minute, DATEDIFF(minute, 0, SnapshotTime), 0)
                                          ORDER BY SnapshotTime DESC) AS rn
                FROM IndicatorSnapshots WITH (NOLOCK)
                WHERE SnapshotTime >= {fromIst})
            SELECT Symbol, SnapshotTime, Pcr, MarketState, TrendDirection, StrategySelected, MarketRegime FROM x WHERE rn = 1")
            .ToListAsync(ct);
    }

    /// <summary>Loads every minute's latest raw indicator snapshot for independent signal discovery.
    /// No historical signal, strategy, confidence, or gate columns are selected or filtered.</summary>
    public async Task<List<DiscoverySnapshotRow>> LoadDiscoverySnapshotsAsync(DateTime fromIst, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return await db.Database.SqlQuery<DiscoverySnapshotRow>($@"
            WITH x AS (
                SELECT Symbol, SnapshotTime, Ltp, DayOpen, YesterdayClose,
                       Ema20, Ema50, Ema20_5m, Ema50_5m, Rsi, Rsi5m,
                       AtrRaw, AtrPct, Pcr, PcrFullChain, Vwap, PriceChangePct,
                       ROW_NUMBER() OVER (
                           PARTITION BY Symbol, DATEADD(minute, DATEDIFF(minute, 0, SnapshotTime), 0)
                           ORDER BY SnapshotTime DESC, Id DESC) AS rn
                FROM IndicatorSnapshots WITH (NOLOCK)
                WHERE SnapshotTime >= {fromIst})
            SELECT Symbol, SnapshotTime, Ltp, DayOpen, YesterdayClose,
                   Ema20, Ema50, Ema20_5m, Ema50_5m, Rsi, Rsi5m,
                   AtrRaw, AtrPct, Pcr, PcrFullChain, Vwap, PriceChangePct
            FROM x WHERE rn = 1 ORDER BY SnapshotTime, Symbol")
            .ToListAsync(ct);
    }

    public async Task<List<OptionMinuteRow>> LoadOptionHistoryAsync(CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return await db.OptionMinuteHistory
            .Select(o => new OptionMinuteRow
            {
                Symbol = o.Symbol, MinuteTime = o.MinuteTime, TradingSymbol = o.TradingSymbol, StrikePrice = o.StrikePrice,
                OptionType = o.OptionType, ExpiryDate = o.ExpiryDate, Ltp = o.Ltp, Delta = o.Delta,
                ChangeInOi = o.ChangeInOi, OpenInterest = o.OpenInterest, UnderlyingLtp = o.UnderlyingLtp,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Real option quotes (IST) the live engine recorded, for pricing entries on days without OptionMinuteHistory:
    /// every Signals row with OptionLtp / DeltaAtEntry, plus the last IndicatorSnapshots row per symbol, minute and option
    /// type that carries BlockedOptionLtp / BlockedDelta. Read-only.
    /// </summary>
    public async Task<List<EntryQuoteRow>> LoadEntryQuotesAsync(DateTime fromIst, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);

        var fromSignals = await db.Signals
            .Where(s => s.CreatedOn >= fromIst && s.OptionLtp > 0 && s.DeltaAtEntry != 0)
            .Select(s => new EntryQuoteRow
            {
                Symbol = s.Symbol, TimeIst = s.CreatedOn, OptionType = s.OptionType,
                Ltp = s.OptionLtp, Delta = s.DeltaAtEntry, Origin = "Signals",
            })
            .ToListAsync(ct);

        var fromSnapshots = await db.Database.SqlQuery<EntryQuoteRow>($@"
            WITH x AS (
                SELECT Symbol, SnapshotTime, BlockedOptionType, BlockedOptionLtp, BlockedDelta,
                       ROW_NUMBER() OVER (
                           PARTITION BY Symbol, BlockedOptionType, DATEADD(minute, DATEDIFF(minute, 0, SnapshotTime), 0)
                           ORDER BY SnapshotTime DESC, Id DESC) AS rn
                FROM IndicatorSnapshots WITH (NOLOCK)
                WHERE SnapshotTime >= {fromIst}
                  AND BlockedOptionLtp > 0 AND BlockedDelta IS NOT NULL AND BlockedDelta <> 0
                  AND BlockedOptionType IS NOT NULL)
            SELECT Symbol, SnapshotTime AS TimeIst, BlockedOptionType AS OptionType,
                   BlockedOptionLtp AS Ltp, BlockedDelta AS Delta, 'IndicatorSnapshots' AS Origin
            FROM x WHERE rn = 1")
            .ToListAsync(ct);

        fromSignals.AddRange(fromSnapshots);
        return fromSignals;
    }

    public async Task<BacktestSettings> LoadSettingsAsync(CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return BacktestSettings.FromRows(await db.StrategySettings.ToListAsync(ct));
    }

    public async Task<List<TradingInstrument>> LoadInstrumentsAsync(CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return await db.TradingInstruments.Where(i => i.IsActive).ToListAsync(ct);
    }

    /// <summary>
    /// The live trading mode (SystemSettings key "TradingMode", the row GrowwAuthService.GetMode reads). Only that one key is
    /// selected - SystemSettings also holds broker credentials. Null when the row is missing or not a TradingMode name.
    /// </summary>
    public async Task<TradingMode?> LoadTradingModeAsync(CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        var value = await db.SystemSettings
            .Where(s => s.Key == "TradingMode")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        return Enum.TryParse<TradingMode>(value, true, out var mode) ? mode : null;
    }

    /// <summary>Date of the earliest stored candle among <paramref name="series"/> (e.g. NIFTY_1m); null when there is none.</summary>
    public async Task<DateTime?> LoadFirstCandleDateAsync(IReadOnlyCollection<string> series, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        var names = series.ToList();
        return await db.Candles
            .Where(c => names.Contains(c.Symbol))
            .MinAsync(c => (DateTime?)c.CandleTime, ct);
    }

    /// <summary>StrategySettings rows whose UpdatedOn is on or after <paramref name="fromIst"/> (values are today's, not the old ones).</summary>
    public async Task<List<StrategySetting>> LoadSettingsChangedSinceAsync(DateTime fromIst, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return await db.StrategySettings
            .Where(s => s.UpdatedOn >= fromIst)
            .OrderBy(s => s.UpdatedOn)
            .ToListAsync(ct);
    }

    /// <summary>Every real signal the live pipeline persisted from <paramref name="fromIst"/> (Signals.CreatedOn is IST).</summary>
    public async Task<List<LiveSignalRow>> LoadLiveSignalsAsync(DateTime fromIst, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return await db.Signals
            .Where(s => s.CreatedOn >= fromIst)
            .OrderBy(s => s.CreatedOn)
            .Select(s => new LiveSignalRow
            {
                Id = s.Id, Symbol = s.Symbol, SignalType = s.SignalType, CreatedOn = s.CreatedOn,
                StrategyType = s.StrategyType, SelectedStrategy = s.SelectedStrategy, MarketState = s.MarketState,
                Status = s.Status, ExitReason = s.ExitReason,
            })
            .ToListAsync(ct);
    }

    /// <summary>Realised P&amp;L per signal: the sum of TradeJournal.ProfitLoss over every row (partial exits included) of that SignalId.</summary>
    public async Task<Dictionary<long, decimal>> LoadLivePnlBySignalAsync(IReadOnlyCollection<long> signalIds, CancellationToken ct = default)
    {
        if (signalIds.Count == 0)
            return [];
        await using var db = new TradingDbContext(_options);
        var ids = signalIds.ToList();
        var rows = await db.TradeJournal
            .Where(t => t.SignalId != null && ids.Contains(t.SignalId.Value))
            .GroupBy(t => t.SignalId!.Value)
            .Select(g => new { SignalId = g.Key, Pnl = g.Sum(t => t.ProfitLoss) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.SignalId, r => r.Pnl);
    }

    /// <summary>
    /// What the live audit (IndicatorSnapshots, one row per symbol per SignalWorker cycle) recorded for each symbol-minute:
    /// the most frequent BlockedBy in that minute (empty = a signal was generated or nothing blocked) and whether any
    /// cycle in the minute generated a signal. Aggregated in SQL so per-second rows never reach the process.
    /// </summary>
    public async Task<List<LiveAuditMinuteRow>> LoadLiveAuditAsync(DateTime fromIst, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        return await db.Database.SqlQuery<LiveAuditMinuteRow>($@"
            WITH m AS (
                SELECT Symbol,
                       DATEADD(minute, DATEDIFF(minute, 0, SnapshotTime), 0) AS MinuteIst,
                       ISNULL(BlockedBy, '') AS BlockedBy,
                       MAX(CAST(SignalGenerated AS int)) AS AnyGenerated,
                       MAX(BlockedReason) AS SampleReason,
                       COUNT(*) AS Cycles
                FROM IndicatorSnapshots WITH (NOLOCK)
                WHERE SnapshotTime >= {fromIst}
                GROUP BY Symbol, DATEADD(minute, DATEDIFF(minute, 0, SnapshotTime), 0), ISNULL(BlockedBy, '')),
            r AS (
                SELECT Symbol, MinuteIst, BlockedBy, SampleReason, Cycles,
                       MAX(AnyGenerated) OVER (PARTITION BY Symbol, MinuteIst) AS AnyGenerated,
                       ROW_NUMBER() OVER (PARTITION BY Symbol, MinuteIst ORDER BY Cycles DESC, BlockedBy) AS rn
                FROM m)
            SELECT Symbol, MinuteIst, BlockedBy, SampleReason, Cycles, AnyGenerated FROM r WHERE rn = 1")
            .ToListAsync(ct);
    }

    /// <summary>Average |delta| and entry premium per symbol from real signals: premium = index move x that delta in the model.</summary>
    public async Task<ExitModelInputs> LoadExitInputsAsync(IReadOnlyList<TradingInstrument> instruments, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        var rows = await db.Signals
            .Where(s => s.OptionLtp > 0 && s.DeltaAtEntry != 0)
            .GroupBy(s => s.Symbol)
            .Select(g => new { Symbol = g.Key, Premium = g.Average(s => s.OptionLtp), Delta = g.Average(s => Math.Abs(s.DeltaAtEntry)) })
            .ToListAsync(ct);

        // Lot size per symbol from EVERY TradingInstruments row with a LotSize (active row preferred), not only the active
        // instruments: an index that is switched off today still traded in full lots on the days being replayed. Without
        // this, ExitModel fell back to a lot of 1 and priced NIFTY/SENSEX trades per single unit.
        var lotRows = await db.TradingInstruments
            .Where(i => i.LotSize > 0)
            .Select(i => new { i.Symbol, i.LotSize, i.IsActive })
            .ToListAsync(ct);
        var lotSizes = lotRows
            .GroupBy(i => i.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.IsActive).First().LotSize, StringComparer.OrdinalIgnoreCase);
        foreach (var i in instruments.Where(i => i.LotSize > 0))
            lotSizes[i.Symbol] = i.LotSize;

        return new ExitModelInputs(
            rows.ToDictionary(r => r.Symbol, r => (double)r.Delta, StringComparer.OrdinalIgnoreCase),
            rows.ToDictionary(r => r.Symbol, r => (double)r.Premium, StringComparer.OrdinalIgnoreCase),
            lotSizes);
    }
}
