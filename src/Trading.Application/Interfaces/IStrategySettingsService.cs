namespace Trading.Application.Interfaces;

/// <summary>
/// DB-backed, in-memory-cached strategy/risk parameters (ATR/RSI/PCR thresholds, multipliers,
/// etc.) — replaces values previously hardcoded in TradingSettings/appsettings so they can be
/// tuned without a redeploy. Never hits SQL Server on the read path; reads always come from the
/// cache loaded/refreshed by ReloadAsync. Lookup order for a given key: exact Symbol match ->
/// Symbol == null (global) -> the default passed by the caller.
/// The underlying table stores only a decimal value; GetInt/GetBool/GetString all derive from
/// that same decimal (int: truncate, bool: non-zero, string: invariant-culture format).
/// </summary>
public interface IStrategySettingsService
{
    decimal GetDecimal(string settingKey, string? symbol, decimal defaultValue);
    int GetInt(string settingKey, string? symbol, int defaultValue);
    bool GetBool(string settingKey, string? symbol, bool defaultValue);
    string GetString(string settingKey, string? symbol, string defaultValue);

    /// <summary>Reloads all active settings from the database into the in-memory cache.</summary>
    Task ReloadAsync(CancellationToken ct = default);
}
