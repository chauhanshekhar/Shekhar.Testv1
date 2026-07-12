using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RiskController : ControllerBase
{
    private const string SettingsKey = "RiskSettings";
    private readonly RiskManagementService _risk;
    private readonly IRepository<SystemSetting> _settingRepo;
    private readonly Trading.Application.Interfaces.IUnitOfWork _uow;

    public RiskController(
        RiskManagementService risk,
        IRepository<SystemSetting> settingRepo,
        Trading.Application.Interfaces.IUnitOfWork uow)
    {
        _risk        = risk;
        _settingRepo = settingRepo;
        _uow         = uow;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var row = await _settingRepo.FirstOrDefaultAsync(s => s.Key == SettingsKey);
        if (row is null) return Ok(new RiskSettings());
        var settings = JsonSerializer.Deserialize<RiskSettings>(row.Value) ?? new RiskSettings();
        return Ok(settings);
    }

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] RiskSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        var row  = await _settingRepo.FirstOrDefaultAsync(s => s.Key == SettingsKey);
        if (row is null)
        {
            await _settingRepo.AddAsync(new SystemSetting { Key = SettingsKey, Value = json });
        }
        else
        {
            row.Value     = json;
            row.UpdatedOn = Trading.Domain.IstClock.Now;
        }
        await _uow.SaveChangesAsync();
        return Ok(settings);
    }

    /// <summary>
    /// GET /api/Risk/check?capital=100000&riskPercent=1&entryPrice=24800&stopLossPrice=24750&lotSize=50
    ///
    /// Returns whether the trade fits within the risk budget, the ₹ risk amount,
    /// and how many lots can be traded.
    /// </summary>
    [HttpGet("check")]
    public IActionResult Check(
        [FromQuery] decimal capital,
        [FromQuery] decimal riskPercent,
        [FromQuery] decimal entryPrice,
        [FromQuery] decimal stopLossPrice,
        [FromQuery] int lotSize = 50)
    {
        var canTrade = _risk.CanTrade(capital, riskPercent, entryPrice, stopLossPrice, lotSize);
        var lotCount = _risk.GetLotCount(capital, riskPercent, entryPrice, stopLossPrice, lotSize);
        var riskBudget = Math.Round(capital * riskPercent / 100m, 2);
        var stopDistance = Math.Round(Math.Abs(entryPrice - stopLossPrice), 2);
        var riskPerLot = Math.Round(stopDistance * lotSize, 2);

        return Ok(new
        {
            canTrade,
            riskBudget,
            stopDistance,
            riskPerLot,
            lotCount,
            riskPercent,
            entryPrice,
            stopLossPrice,
            lotSize
        });
    }
}

public class RiskSettings
{
    public decimal MaxCapitalRiskPercent { get; set; } = 2;
    public decimal MaxDailyLoss { get; set; } = 5000;
    public int MaxLotSize { get; set; } = 5;
    public int MaxOpenPositions { get; set; } = 3;
    public decimal MinRiskRewardRatio { get; set; } = 1.5m;
    public bool TradingEnabled { get; set; } = true;
}
