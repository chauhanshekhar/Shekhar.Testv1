using Microsoft.EntityFrameworkCore;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Data;
using Trading.Backtest.Reporting;

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

    public async Task<List<Bar>> LoadBarsAsync(string series, DateTime fromUtc, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        var rows = await db.Candles
            .Where(c => c.Symbol == series && c.CandleTime >= fromUtc)
            .OrderBy(c => c.CandleTime)
            .Select(c => new { c.CandleTime, c.Open, c.High, c.Low, c.Close })
            .ToListAsync(ct);
        return rows.Select(c => new Bar(c.CandleTime, (double)c.Open, (double)c.High, (double)c.Low, (double)c.Close)).ToList();
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

    /// <summary>Average |delta| and entry premium per symbol from real signals: premium = index move x that delta in the model.</summary>
    public async Task<ExitModelInputs> LoadExitInputsAsync(IReadOnlyList<TradingInstrument> instruments, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(_options);
        var rows = await db.Signals
            .Where(s => s.OptionLtp > 0 && s.DeltaAtEntry != 0)
            .GroupBy(s => s.Symbol)
            .Select(g => new { Symbol = g.Key, Premium = g.Average(s => s.OptionLtp), Delta = g.Average(s => Math.Abs(s.DeltaAtEntry)) })
            .ToListAsync(ct);

        return new ExitModelInputs(
            rows.ToDictionary(r => r.Symbol, r => (double)r.Delta, StringComparer.OrdinalIgnoreCase),
            rows.ToDictionary(r => r.Symbol, r => (double)r.Premium, StringComparer.OrdinalIgnoreCase),
            instruments.ToDictionary(i => i.Symbol, i => i.LotSize, StringComparer.OrdinalIgnoreCase));
    }
}
