using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

/// <summary>
/// Single entry point for the runtime operational switches stored in SystemSettings —
/// AutoTradingEnabled, TradingMode, GrowwAccessToken. Strategy/risk/scoring configuration
/// lives in TradingSettings (appsettings-backed) and is not part of this service.
/// </summary>
public interface IRuntimeSettingsService
{
    bool IsAutoTradingEnabled();
    TradingMode GetTradingMode();
    string GetGrowwAccessToken();
}
