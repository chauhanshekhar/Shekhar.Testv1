using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Application.Interfaces;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Super Admin")]
public class AdminController : ControllerBase
{
    private readonly IRepository<User> _users;
    private readonly IRepository<Signal> _signals;
    private readonly IRepository<TradeJournal> _trades;
    private readonly IRepository<PaperTrade> _paperTrades;
    private readonly IRepository<SystemSetting> _settings;
    private readonly IUnitOfWork _uow;

    public AdminController(
        IRepository<User> users,
        IRepository<Signal> signals,
        IRepository<TradeJournal> trades,
        IRepository<PaperTrade> paperTrades,
        IRepository<SystemSetting> settings,
        IUnitOfWork uow)
    {
        _users = users;
        _signals = signals;
        _trades = trades;
        _paperTrades = paperTrades;
        _settings = settings;
        _uow = uow;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var todayUtc = IstClock.Now.Date;
        var weekAgoUtc = todayUtc.AddDays(-7);

        var users = await _users.FindAsync(_ => true);
        var todaySigs = await _signals.FindAsync(s => s.CreatedOn >= todayUtc);
        var todayTrades = await _trades.FindAsync(t => t.TradeDate >= todayUtc);
        var weekSignals = await _signals.FindAsync(s => s.CreatedOn >= weekAgoUtc);

        return Ok(new
        {
            TotalUsers = users.Count,
            ActiveUsers = users.Count(u => u.IsActive),
            TodaySignals = todaySigs.Count,
            TodayTrades = todayTrades.Count,
            WeekSignals = weekSignals.Count,
            BuySignals = weekSignals.Count(s => s.SignalType == TradingConst.SignalType.CALL),
            SellSignals = weekSignals.Count(s => s.SignalType == TradingConst.SignalType.PUT)
        });
    }

    [HttpGet("signals/recent")]
    public async Task<IActionResult> RecentSignals([FromQuery] int count = 20)
    {
        var cutoff = IstClock.Now.AddDays(-7);
        var signals = await _signals.FindAsync(s => s.CreatedOn >= cutoff);
        return Ok(signals.OrderByDescending(s => s.CreatedOn).Take(count));
    }

    // ── Kill switch ─────────────────────────────────────────────────────────

    /// <summary>GET /api/admin/killswitch — returns current AutoTradingEnabled state</summary>
    [HttpGet("killswitch")]
    public async Task<IActionResult> GetKillSwitch()
    {
        var setting = await _settings.FirstOrDefaultAsync(s => s.Key == "AutoTradingEnabled");
        var enabled = setting == null || !setting.Value.Equals("false", StringComparison.OrdinalIgnoreCase);
        return Ok(new { AutoTradingEnabled = enabled, UpdatedOn = setting?.UpdatedOn });
    }

    /// <summary>POST /api/admin/killswitch?enabled=false — toggle auto-trading on/off</summary>
    [HttpPost("killswitch")]
    public async Task<IActionResult> SetKillSwitch([FromQuery] bool enabled)
    {
        var setting = await _settings.FirstOrDefaultAsync(s => s.Key == "AutoTradingEnabled");
        if (setting == null)
        {
            await _settings.StageAsync(new SystemSetting
            {
                Key = "AutoTradingEnabled",
                Value = enabled ? "true" : "false",
                UpdatedOn = IstClock.Now
            });
        }
        else
        {
            setting.Value = enabled ? "true" : "false";
            setting.UpdatedOn = IstClock.Now;
        }

        await _uow.SaveChangesAsync();
        return Ok(new { AutoTradingEnabled = enabled, UpdatedOn = IstClock.Now });
    }
}
