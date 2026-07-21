using Trading.Domain;

namespace Trading.Domain.Entities;

/// <summary>
/// DB-driven strategy/risk parameter (ATR/RSI/PCR thresholds, multipliers, etc.), replacing
/// values previously hardcoded in TradingSettings/appsettings. Read path is StrategySettingsService,
/// which caches all active rows in memory — never queried directly during signal generation.
/// Lookup order for a given key: exact Symbol match -> Symbol == null (global) -> caller default.
/// </summary>
public class StrategySetting
{
    public int Id { get; set; }
    public string SettingKey { get; set; } = "";
    public string? Symbol { get; set; }
    public decimal SettingValue { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public DateTime UpdatedOn { get; set; } = IstClock.Now;
}
