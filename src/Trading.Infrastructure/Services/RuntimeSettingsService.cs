using Microsoft.EntityFrameworkCore;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Services;

/// <summary>
/// GetTradingMode/GetGrowwAccessToken delegate to the existing IGrowwAuthService (no
/// duplicated DB-read logic). IsAutoTradingEnabled is the one genuinely new read here,
/// replicating the "missing row = enabled" default that KillSwitchGate/AdminController
/// implemented ad-hoc before this service existed.
/// </summary>
public class RuntimeSettingsService : IRuntimeSettingsService
{
    private const string AutoTradingKey = "AutoTradingEnabled";

    private readonly IDbContextFactory<TradingDbContext> _dbFactory;
    private readonly IGrowwAuthService _growwAuth;

    public RuntimeSettingsService(
        IDbContextFactory<TradingDbContext> dbFactory,
        IGrowwAuthService growwAuth)
    {
        _dbFactory = dbFactory;
        _growwAuth = growwAuth;
    }

    public bool IsAutoTradingEnabled()
    {
        using var db = _dbFactory.CreateDbContext();
        var row = db.SystemSettings.AsNoTracking().FirstOrDefault(s => s.Key == AutoTradingKey);
        return row == null || !row.Value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    public TradingMode GetTradingMode() => _growwAuth.GetMode();

    public string GetGrowwAccessToken() => _growwAuth.GetAccessToken();
}
