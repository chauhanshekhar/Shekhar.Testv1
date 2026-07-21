using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.SignalEngine.Services;

/// <summary>
/// Maintains the StrategyPerformance rollup — one row per (Strategy, MarketState, Expiry, Hour)
/// bucket, recomputed after every completed (non-partial) trade. Reuses PerformanceAnalyticsService's
/// formulas verbatim. Read by StrategySelectorService (GetWinRateAsync) to weight strategy choice,
/// gated by TradingSettings.MinTradesForHistoricalWeight so a handful of trades never overfits.
/// </summary>
public class StrategyPerformanceService
{
    private readonly IRepository<TradeJournal> _journalRepo;
    private readonly IRepository<StrategyPerformance> _perfRepo;
    private readonly PerformanceAnalyticsService _perf;
    private readonly SystemSettingsService _systemSettings;
    private readonly IUnitOfWork _uow;

    public StrategyPerformanceService(
        IRepository<TradeJournal> journalRepo,
        IRepository<StrategyPerformance> perfRepo,
        PerformanceAnalyticsService perf,
        SystemSettingsService systemSettings,
        IUnitOfWork uow)
    {
        _journalRepo = journalRepo;
        _perfRepo = perfRepo;
        _perf = perf;
        _systemSettings = systemSettings;
        _uow = uow;
    }

    public async Task RecordCompletedTradeAsync(TradeJournal trade)
    {
        if (string.IsNullOrEmpty(trade.SelectedStrategy))
            return; // no strategy context on this trade — nothing to bucket

        var bucket = (await _journalRepo.FindAsync(t =>
                t.SelectedStrategy == trade.SelectedStrategy &&
                t.MarketState == trade.MarketState &&
                t.IsExpiryDay == trade.IsExpiryDay &&
                t.EntryHour == trade.EntryHour &&
                !t.IsPartial))
            .OrderBy(t => t.TradeDate)
            .ToList();

        var wins = bucket.Count(t => t.ProfitLoss > 0);
        var losses = bucket.Count(t => t.ProfitLoss < 0);
        var grossProfit = bucket.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss);
        var grossLoss = Math.Abs(bucket.Where(t => t.ProfitLoss < 0).Sum(t => t.ProfitLoss));

        // Running PnL as this bucket's own equity curve, for its own MaxDrawdown.
        decimal running = 0;
        var equityCurve = bucket.Select(t => running += t.ProfitLoss).ToList();

        var row = await _perfRepo.FirstOrDefaultAsync(p =>
            p.Strategy == trade.SelectedStrategy &&
            p.MarketState == trade.MarketState &&
            p.Expiry == trade.IsExpiryDay &&
            p.Hour == trade.EntryHour);

        var isNew = row == null;
        row ??= new StrategyPerformance
        {
            Strategy = trade.SelectedStrategy,
            MarketState = trade.MarketState,
            Expiry = trade.IsExpiryDay,
            Hour = trade.EntryHour
        };

        row.Trades = bucket.Count;
        row.Wins = wins;
        row.Losses = losses;
        row.WinRate = _perf.WinRate(wins, losses);
        row.ProfitFactor = _perf.ProfitFactor(grossProfit, grossLoss);
        row.AverageWin = _perf.AverageWin(bucket);
        row.AverageLoss = _perf.AverageLoss(bucket);
        row.Expectancy = _perf.ExpectancyPerTrade(bucket);
        row.NetPnL = grossProfit - grossLoss;
        row.MaxDrawdown = _perf.MaxDrawdown(equityCurve);
        row.LastUpdated = IstClock.Now;

        if (isNew)
            await _perfRepo.AddAsync(row);
        else
            await _perfRepo.UpdateAsync(row);

        await _uow.SaveChangesAsync();
    }

    /// <summary>Null when the bucket doesn't exist yet or hasn't reached MinTradesForHistoricalWeight.</summary>
    public async Task<(decimal WinRate, int Trades)?> GetWinRateAsync(string strategy, string marketState, bool isExpiry, int hour)
    {
        var settings = await _systemSettings.GetAsync();

        var row = await _perfRepo.FirstOrDefaultAsync(p =>
            p.Strategy == strategy && p.MarketState == marketState && p.Expiry == isExpiry && p.Hour == hour);

        if (row == null || row.Trades < settings.MinTradesForHistoricalWeight)
            return null;

        return (row.WinRate, row.Trades);
    }
}
