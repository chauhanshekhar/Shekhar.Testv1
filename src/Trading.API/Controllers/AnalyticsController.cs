using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IRepository<TradeJournal> _tradeRepo;
    private readonly IRepository<DailyPnL> _dailyPnlRepo;
    private readonly PerformanceAnalyticsService _analytics;

    public AnalyticsController(
        IRepository<TradeJournal> tradeRepo,
        IRepository<DailyPnL> dailyPnlRepo,
        PerformanceAnalyticsService analytics)
    {
        _tradeRepo = tradeRepo;
        _dailyPnlRepo = dailyPnlRepo;
        _analytics = analytics;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var cutoff = IstClock.Now.AddDays(-90);
        var trades = await _tradeRepo.FindAsync(t => t.TradeDate >= cutoff);
        var dailyPnl = await _dailyPnlRepo.FindAsync(d => d.TradeDate >= cutoff);
        var summary = _analytics.BuildSummary(trades, dailyPnl);
        return Ok(summary);
    }

    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var todayUtc = IstClock.Now.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var todayRecord = await _dailyPnlRepo.FirstOrDefaultAsync(
            d => d.TradeDate >= todayUtc && d.TradeDate < tomorrowUtc);

        return Ok(new
        {
            Date = todayUtc,
            NetPnL = todayRecord?.NetProfit ?? 0,
            TotalProfit = todayRecord?.TotalProfit ?? 0,
            TotalLoss = todayRecord?.TotalLoss ?? 0,
            Trades = todayRecord?.TotalTrades ?? 0
        });
    }

    [HttpGet("daily")]
    public async Task<IActionResult> Daily([FromQuery] int days = 30)
    {
        var cutoff = IstClock.Now.Date.AddDays(-days);
        var dailyPnl = await _dailyPnlRepo.FindAsync(d => d.TradeDate >= cutoff);
        var result = dailyPnl
            .OrderBy(d => d.TradeDate)
            .Select(d => new
            {
                Date = d.TradeDate.ToString("yyyy-MM-dd"),
                d.NetProfit,
                d.TotalProfit,
                d.TotalLoss,
                d.TotalTrades
            });

        return Ok(result);
    }

    [HttpGet("trades")]
    public async Task<IActionResult> Trades([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        if (size > 200) size = 200;
        var cutoff = IstClock.Now.AddDays(-90);
        var trades = await _tradeRepo.FindAsync(t => t.TradeDate >= cutoff);
        var ordered = trades.OrderByDescending(t => t.TradeDate).ToList();
        var paged = ordered.Skip((page - 1) * size).Take(size);

        return Ok(new { Total = ordered.Count, Data = paged });
    }

    // ── Win-Rate Analytics ───────────────────────────────────────────────────

    /// <summary>GET /api/analytics/winrate — Win rate by Symbol, Day-of-week, Hour, and TradeType (strategy)</summary>
    [HttpGet("winrate")]
    public async Task<IActionResult> WinRate([FromQuery] int days = 90)
    {
        var cutoff = IstClock.Now.Date.AddDays(-days);
        var trades = await _tradeRepo.FindAsync(t => t.TradeDate >= cutoff);

        if (trades.Count == 0)
            return Ok(new { BySymbol = Array.Empty<object>(), ByDay = Array.Empty<object>(), ByHour = Array.Empty<object>(), ByStrategy = Array.Empty<object>() });

        static object WinRateRow(string label, IEnumerable<TradeJournal> items)
        {
            var list      = items.ToList();
            var total     = list.Count;
            var wins      = list.Count(t => t.ProfitLoss > 0);
            var losses    = list.Count(t => t.ProfitLoss < 0);
            var breakeven = list.Count(t => t.ProfitLoss == 0);
            var pnl       = list.Sum(t => t.ProfitLoss);
            return new
            {
                Label      = label,
                Trades     = total,
                Wins       = wins,
                Losses     = losses,
                Breakeven  = breakeven,
                WinRate    = total > 0 ? Math.Round((double)wins / total * 100, 1) : 0.0,
                TotalPnL   = Math.Round(pnl, 2),
                AvgPnL     = total > 0 ? Math.Round(pnl / total, 2) : 0m,
            };
        }

        var bySymbol = trades
            .GroupBy(t => t.Symbol)
            .Select(g => WinRateRow(g.Key, g))
            .OrderByDescending(r => ((dynamic)r).TotalPnL)
            .ToList();

        var byDay = trades
            .GroupBy(t => t.TradeDate.DayOfWeek.ToString())
            .Select(g => WinRateRow(g.Key, g))
            .ToList();

        var byHour = trades
            .GroupBy(t => t.TradeDate.Hour.ToString("D2") + ":00")
            .Select(g => WinRateRow(g.Key, g))
            .OrderBy(r => ((dynamic)r).Label)
            .ToList();

        // Group by StrategyType (SINGLE/SPREAD); fall back to TradeType for old rows
        var byStrategy = trades
            .GroupBy(t => !string.IsNullOrEmpty(t.StrategyType) ? t.StrategyType
                        : !string.IsNullOrEmpty(t.TradeType)    ? t.TradeType
                        : "UNKNOWN")
            .Select(g => WinRateRow(g.Key, g))
            .OrderByDescending(r => ((dynamic)r).WinRate)
            .ToList();

        return Ok(new { BySymbol = bySymbol, ByDay = byDay, ByHour = byHour, ByStrategy = byStrategy });
    }

    /// <summary>GET /api/analytics/best-worst — Top 5 best and worst performing symbols</summary>
    [HttpGet("best-worst")]
    public async Task<IActionResult> BestWorst([FromQuery] int days = 90)
    {
        var cutoff = IstClock.Now.Date.AddDays(-days);
        var trades = await _tradeRepo.FindAsync(t => t.TradeDate >= cutoff);

        var bySymbol = trades
            .GroupBy(t => t.Symbol)
            .Select(g => new
            {
                Symbol   = g.Key,
                Trades   = g.Count(),
                Wins     = g.Count(t => t.ProfitLoss > 0),
                TotalPnL = Math.Round(g.Sum(t => t.ProfitLoss), 2),
                WinRate  = g.Count() > 0 ? Math.Round((double)g.Count(t => t.ProfitLoss > 0) / g.Count() * 100, 1) : 0.0,
            })
            .ToList();

        return Ok(new
        {
            Best  = bySymbol.OrderByDescending(s => s.TotalPnL).Take(5),
            Worst = bySymbol.OrderBy(s => s.TotalPnL).Take(5),
        });
    }
}
