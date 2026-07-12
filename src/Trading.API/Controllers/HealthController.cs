using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trading.Domain;
using Trading.Infrastructure.Data;

namespace Trading.API.Controllers;

/// <summary>
/// GET /api/health/dashboard — live operational status of the trading system.
/// Shows last data times, worker liveliness, and DB status.
/// </summary>
[ApiController]
[Route("api/health")]
[Authorize(Roles = "Admin,Super Admin")]
public class HealthController : ControllerBase
{
    private readonly IDbContextFactory<TradingDbContext> _dbFactory;

    // Thresholds for liveliness checks (during market hours)
    private static readonly TimeSpan TickStale         = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OptionChainStale  = TimeSpan.FromMinutes(5);

    public HealthController(IDbContextFactory<TradingDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = IstClock.Now;
        var isMarketOpen = MarketHours.IsOpen();

        // ── Last tick time ───────────────────────────────────────────────────
        var lastTick = await db.MarketTicks
            .OrderByDescending(t => t.TickTime)
            .Select(t => (DateTime?)t.TickTime)
            .FirstOrDefaultAsync();

        var tickAgeMin      = lastTick.HasValue ? (now - lastTick.Value).TotalMinutes : (double?)null;
        var tickWorkerOk    = !isMarketOpen || (tickAgeMin.HasValue && tickAgeMin < TickStale.TotalMinutes);

        // ── Last option chain time ───────────────────────────────────────────
        var lastOptionChain = await db.OptionChainData
            .OrderByDescending(o => o.CreatedOn)
            .Select(o => (DateTime?)o.CreatedOn)
            .FirstOrDefaultAsync();

        var optionChainAgeMin   = lastOptionChain.HasValue ? (now - lastOptionChain.Value).TotalMinutes : (double?)null;
        var optionChainWorkerOk = !isMarketOpen || (optionChainAgeMin.HasValue && optionChainAgeMin < OptionChainStale.TotalMinutes);

        // ── Last signal time ─────────────────────────────────────────────────
        var lastSignal = await db.Signals
            .OrderByDescending(s => s.CreatedOn)
            .Select(s => (DateTime?)s.CreatedOn)
            .FirstOrDefaultAsync();

        // ── Open signals ─────────────────────────────────────────────────────
        var openSignals = await db.Signals
            .Where(s => s.Status == "ORDER_PLACED")
            .CountAsync();

        // ── Today PnL ────────────────────────────────────────────────────────
        var todayPnl = await db.DailyPnL
            .Where(p => p.TradeDate == IstClock.Today)
            .Select(p => new { p.RealizedPnl, p.UnrealizedPnl, p.DailyDrawdown })
            .FirstOrDefaultAsync();

        // ── DB status ────────────────────────────────────────────────────────
        bool dbOk;
        try { dbOk = await db.Database.CanConnectAsync(); }
        catch { dbOk = false; }

        // ── Kill switch ──────────────────────────────────────────────────────
        var killSetting = await db.SystemSettings
            .Where(s => s.Key == "AutoTradingEnabled")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        var autoTradingEnabled = killSetting == null
            || !killSetting.Equals("false", StringComparison.OrdinalIgnoreCase);

        // ── Holiday today ────────────────────────────────────────────────────
        var todayHoliday = await db.ExchangeHolidays
            .Where(h => h.IsActive && h.HolidayDate == IstClock.Today)
            .Select(h => h.Description)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            ServerTime      = now.ToString("yyyy-MM-dd HH:mm:ss") + " IST",
            MarketOpen      = isMarketOpen,
            TodayHoliday    = todayHoliday,
            AutoTrading     = autoTradingEnabled,
            OpenSignals     = openSignals,

            Workers = new
            {
                MarketFeed = new
                {
                    LastTickTime = lastTick?.ToString("HH:mm:ss"),
                    AgeMinutes   = tickAgeMin.HasValue ? Math.Round(tickAgeMin.Value, 1) : (double?)null,
                    Status       = tickWorkerOk ? "OK" : "STALE"
                },
                OptionChain = new
                {
                    LastFetchTime = lastOptionChain?.ToString("HH:mm:ss"),
                    AgeMinutes    = optionChainAgeMin.HasValue ? Math.Round(optionChainAgeMin.Value, 1) : (double?)null,
                    Status        = optionChainWorkerOk ? "OK" : "STALE"
                },
                Signal = new
                {
                    LastSignalTime = lastSignal?.ToString("yyyy-MM-dd HH:mm:ss"),
                }
            },

            Database = new { Status = dbOk ? "OK" : "ERROR" },

            TodayPnL = todayPnl == null ? null : new
            {
                Realized   = todayPnl.RealizedPnl,
                Unrealized = todayPnl.UnrealizedPnl,
                Drawdown   = todayPnl.DailyDrawdown,
            }
        });
    }

    // ── /api/health/trading ──────────────────────────────────────────────────

    [HttpGet("trading")]
    public async Task<IActionResult> Trading()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var now            = IstClock.Now;
        var isMarketOpen   = MarketHours.IsOpen();

        var lastTick = await db.MarketTicks
            .OrderByDescending(t => t.TickTime)
            .Select(t => (DateTime?)t.TickTime)
            .FirstOrDefaultAsync();

        var lastChain = await db.OptionChainData
            .OrderByDescending(o => o.CreatedOn)
            .Select(o => (DateTime?)o.CreatedOn)
            .FirstOrDefaultAsync();

        var lastSignal = await db.Signals
            .OrderByDescending(s => s.CreatedOn)
            .Select(s => (DateTime?)s.CreatedOn)
            .FirstOrDefaultAsync();

        var tickAgeSec   = lastTick.HasValue   ? Math.Round((now - lastTick.Value).TotalSeconds,  0) : (double?)null;
        var chainAgeSec  = lastChain.HasValue  ? Math.Round((now - lastChain.Value).TotalSeconds, 0) : (double?)null;
        var signalAgeMin = lastSignal.HasValue ? Math.Round((now - lastSignal.Value).TotalMinutes,1) : (double?)null;

        string FeedStatus() =>
            !isMarketOpen     ? "Market Closed" :
            tickAgeSec == null ? "No Data" :
            tickAgeSec < 120  ? "Running" : "Stale";

        string ChainStatus() =>
            !isMarketOpen      ? "Market Closed" :
            chainAgeSec == null ? "No Data" :
            chainAgeSec < 300  ? "Running" : "Stale";

        string SignalStatus() =>
            !isMarketOpen       ? "Market Closed" :
            signalAgeMin == null ? "No Data" :
            signalAgeMin < 60   ? "Running" : "Idle";

        return Ok(new
        {
            LastTickAgeSeconds        = tickAgeSec,
            LastOptionChainAgeSeconds = chainAgeSec,
            LastSignalAgeMinutes      = signalAgeMin,
            MarketFeedWorker          = FeedStatus(),
            OptionChainWorker         = ChainStatus(),
            SignalWorker              = SignalStatus(),
            ExitWorker                = FeedStatus(),
            MarketOpen                = isMarketOpen,
            ServerTime                = now.ToString("yyyy-MM-dd HH:mm:ss") + " IST",
        });
    }
}
