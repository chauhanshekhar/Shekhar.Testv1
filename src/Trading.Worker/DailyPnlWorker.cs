using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;

namespace Trading.Worker;

/// <summary>
/// Aggregates today's closed trades into a single DailyPnL row every minute.
///
/// Stored metrics:
///   TotalProfit / TotalLoss / NetProfit / TotalTrades
///   WinTrades / LossTrades / WinRate / ProfitFactor / MaxDrawdown
///
/// These power the Analytics dashboard, Strategy Performance card, and Admin reports.
/// </summary>
public class DailyPnlWorker : BackgroundService
{
    private readonly ILogger<DailyPnlWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyPnlWorker(
        ILogger<DailyPnlWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await AggregateAsync(); }
            catch (Exception ex)
            { _logger.LogError(ex, "DailyPnlWorker error"); }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    // ── Aggregation ───────────────────────────────────────────────────────────

    private async Task AggregateAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var tradeRepo  = scope.ServiceProvider.GetRequiredService<IRepository<TradeJournal>>();
        var paperRepo  = scope.ServiceProvider.GetRequiredService<IRepository<PaperTrade>>();
        var signalRepo = scope.ServiceProvider.GetRequiredService<IRepository<Signal>>();
        var tickRepo   = scope.ServiceProvider.GetRequiredService<IRepository<MarketTick>>();
        var optionRepo = scope.ServiceProvider.GetRequiredService<IRepository<OptionChainData>>();
        var pnlRepo    = scope.ServiceProvider.GetRequiredService<IRepository<DailyPnL>>();
        var uow        = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Use IST date so 00:15 IST is still "today", not "tomorrow"
        var today = TradingConstants.NowIst().Date;

        var todayStart = today;
        var todayEnd   = today.AddDays(1);
        var trades = await tradeRepo.FindAsync(
            t => t.TradeDate >= todayStart && t.TradeDate < todayEnd);

        // ── Realized P&L (closed trades) ─────────────────────────────────────
        var grossProfit = trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss);
        var grossLoss   = Math.Abs(trades.Where(t => t.ProfitLoss < 0).Sum(t => t.ProfitLoss));
        var realizedPnl = grossProfit - grossLoss;

        // ── Performance metrics ───────────────────────────────────────────────
        var winTrades       = trades.Count(t => t.ProfitLoss > 0);
        var lossTrades      = trades.Count(t => t.ProfitLoss < 0);
        var breakevenTrades = trades.Count(t => t.ProfitLoss == 0);
        var winRate         = trades.Count > 0
            ? Math.Round((decimal)winTrades / trades.Count * 100, 2)
            : 0m;
        var profitFactor = grossLoss > 0
            ? Math.Round(grossProfit / grossLoss, 4)
            : 0m;
        var maxDrawdown = ComputeMaxDrawdown(trades);

        // ── Unrealized P&L (open positions MTM) ──────────────────────────────
        var openTrades = await paperRepo.FindAsync(p => p.Status == "OPEN");
        var unrealizedPnl = 0m;

        var tickCutoff  = IstClock.Now.AddMinutes(-5);
        var recentTicks = await tickRepo.FindAsync(t => t.TickTime >= tickCutoff);
        var ltpBySymbol = recentTicks
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TickTime).First().LastPrice);

        // Fallback source when no recent MarketTick exists — latest OptionChain snapshot LTP.
        var chainSnapshotCutoff = IstClock.Now.AddMinutes(-5);
        var latestSnapshotId = (await optionRepo.FindAsync(o => o.CreatedOn >= chainSnapshotCutoff))
            .OrderByDescending(o => o.CreatedOn)
            .Select(o => o.SnapshotId)
            .FirstOrDefault();
        var chainLtpBySymbol = string.IsNullOrEmpty(latestSnapshotId)
            ? new Dictionary<string, decimal>()
            : (await optionRepo.FindAsync(o => o.SnapshotId == latestSnapshotId))
                .GroupBy(o => o.TradingSymbol)
                .ToDictionary(g => g.Key, g => g.First().Ltp);

        // Paper trades unrealized P&L
        foreach (var p in openTrades)
        {
            var sym = !string.IsNullOrEmpty(p.OptionSymbol) ? p.OptionSymbol : p.Symbol;
            if (!ltpBySymbol.TryGetValue(sym, out var currentLtp)
                && !chainLtpBySymbol.TryGetValue(sym, out currentLtp))
                continue;

            var side = p.Side?.Equals("CALL", StringComparison.OrdinalIgnoreCase) == true ? 1m : -1m;
            var leg1Pnl = (currentLtp - p.EntryPrice) * p.Quantity * side;

            var leg2Pnl = 0m;
            if (!string.IsNullOrEmpty(p.Leg2OptionSymbol) && p.Leg2EntryPrice > 0
                && (ltpBySymbol.TryGetValue(p.Leg2OptionSymbol, out var leg2Ltp)
                    || chainLtpBySymbol.TryGetValue(p.Leg2OptionSymbol, out leg2Ltp)))
            {
                leg2Pnl = (p.Leg2EntryPrice - leg2Ltp) * p.Quantity;
            }

            unrealizedPnl += leg1Pnl + leg2Pnl;
        }

        // Live signal unrealized P&L (non-paper mode): open ORDER_PLACED signals
        // Live signal unrealized P&L (non-paper mode): open ORDER_PLACED signals
        // Live signal unrealized P&L (non-paper mode)
        if (openTrades.Count == 0)
        {
            var openSignals = await signalRepo.FindAsync(
   s =>
   (
       s.Status == SignalStatus.OrderPlaced ||
       s.Status == SignalStatus.Active ||
       s.Status == SignalStatus.PartialExitPending
   )
   && s.CreatedOn >= todayStart);

            foreach (var s in openSignals)
            {
                if (string.IsNullOrEmpty(s.OptionSymbol))
                    continue;


                if (!ltpBySymbol.TryGetValue(s.OptionSymbol, out var optionLtp)
                    && !chainLtpBySymbol.TryGetValue(s.OptionSymbol, out optionLtp))
                    continue;


                var entry =
                    s.OptionLtp > 0
                    ? s.OptionLtp
                    : s.EntryPrice;


                var openLots =
                    s.RemainingQty > 0
                    ? s.RemainingQty
                    : s.Quantity;


                var pnl =
                    (optionLtp - entry)
                    *
                    openLots;


                unrealizedPnl += pnl;
            }
        }

        unrealizedPnl = Math.Round(unrealizedPnl, 2);

        // ── Daily Drawdown — worst of historical DD or current open-position loss ─
        var openPositionLoss = Math.Max(0m, -unrealizedPnl);
        var dailyDrawdown    = Math.Max(maxDrawdown, openPositionLoss);

        // ── Upsert DailyPnL row ───────────────────────────────────────────────
        var existing = (await pnlRepo.FindAsync(p => p.TradeDate == today)).FirstOrDefault();

        if (existing is null)
        {
            await pnlRepo.AddAsync(new DailyPnL
            {
                TradeDate       = today,
                TotalProfit     = grossProfit,
                TotalLoss       = grossLoss,
                NetProfit       = realizedPnl,
                TotalTrades     = trades.Count,
                WinTrades       = winTrades,
                LossTrades      = lossTrades,
                BreakevenTrades = breakevenTrades,
                WinRate         = winRate,
                ProfitFactor    = profitFactor,
                MaxDrawdown     = maxDrawdown,
                RealizedPnl     = realizedPnl,
                UnrealizedPnl   = unrealizedPnl,
                DailyDrawdown   = dailyDrawdown,
                UpdatedOn       = IstClock.Now,
            });
        }
        else
        {
            existing.TotalProfit     = grossProfit;
            existing.TotalLoss       = grossLoss;
            existing.NetProfit       = realizedPnl;
            existing.TotalTrades     = trades.Count;
            existing.WinTrades       = winTrades;
            existing.LossTrades      = lossTrades;
            existing.BreakevenTrades = breakevenTrades;
            existing.WinRate         = winRate;
            existing.ProfitFactor    = profitFactor;
            existing.MaxDrawdown     = maxDrawdown;
            existing.RealizedPnl     = realizedPnl;
            existing.UnrealizedPnl   = unrealizedPnl;
            existing.DailyDrawdown   = dailyDrawdown;
            existing.UpdatedOn       = IstClock.Now;

            await pnlRepo.UpdateAsync(existing);
        }

        await uow.SaveChangesAsync();

        _logger.LogInformation(
            "DailyPnL {Date:d}: Realized ₹{R:N0} | Unrealized ₹{U:N0} | DD ₹{DD:N0} | Win {Win}/{Total} ({Rate:F1}%)",
            today, realizedPnl, unrealizedPnl, dailyDrawdown, winTrades, trades.Count, winRate);
    }

    // ── Max Drawdown ──────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the maximum peak-to-trough decline in running P&L for the day.
    /// Trades are ordered chronologically; the result is a positive value in ₹.
    ///
    /// Example:
    ///   Trades: +500, +300, -800, +200, -1000, +400
    ///   Running: 500, 800, 0, 200, -800, -400
    ///   Peak: 800   Trough after peak: -800   MaxDrawdown: 1600
    /// </summary>
    private static decimal ComputeMaxDrawdown(List<TradeJournal> trades)
    {
        if (trades.Count == 0) return 0m;

        var ordered = trades.OrderBy(t => t.TradeDate).ToList();
        var running = 0m;
        var peak = 0m;
        var maxDD = 0m;

        foreach (var t in ordered)
        {
            running += t.ProfitLoss;
            if (running > peak)
                peak = running;
            var drawdown = peak - running;
            if (drawdown > maxDD)
                maxDD = drawdown;
        }

        return Math.Round(maxDD, 2);
    }
}
