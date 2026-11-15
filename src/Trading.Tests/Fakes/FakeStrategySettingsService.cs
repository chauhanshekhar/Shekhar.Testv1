using Trading.Application.Interfaces;

namespace Trading.Tests.Fakes;

/// <summary>
/// In-memory stand-in for IStrategySettingsService — set only the keys a test cares about,
/// everything else falls back to the caller's own default (same semantics as the real
/// DB-backed cache when no row exists for a key). Symbol is ignored — tests are single-symbol.
///
/// IsSettingActive mirrors the real StrategySettingsService: a key is active only once it has
/// been Set (i.e. a row "exists") AND not SetInactive. A key that was never Set is NOT active —
/// matching the real service's "no row -> not active" rule — even though GetDecimal/GetBool
/// still return the caller's own default for it, exactly as the real service does.
/// </summary>
public class FakeStrategySettingsService : IStrategySettingsService
{
    private readonly Dictionary<string, decimal> _values = new(StringComparer.OrdinalIgnoreCase);

    public FakeStrategySettingsService Set(string key, decimal value)
    {
        _values[key] = value;
        return this;
    }

    public decimal GetDecimal(string settingKey, string? symbol, decimal defaultValue) =>
        _values.TryGetValue(settingKey, out var v) ? v : defaultValue;

    public int GetInt(string settingKey, string? symbol, int defaultValue) =>
        _values.TryGetValue(settingKey, out var v) ? (int)v : defaultValue;

    public bool GetBool(string settingKey, string? symbol, bool defaultValue) =>
        _values.TryGetValue(settingKey, out var v) ? v != 0m : defaultValue;

    public string GetString(string settingKey, string? symbol, string defaultValue) =>
        _values.TryGetValue(settingKey, out var v) ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : defaultValue;

    /// <summary>Keys added via SetInactive are treated as switched-off rows.</summary>
    private readonly HashSet<string> _inactive = new(StringComparer.OrdinalIgnoreCase);

    public FakeStrategySettingsService SetInactive(string key)
    {
        _inactive.Add(key);
        return this;
    }

    public bool IsSettingActive(string settingKey, string? symbol) =>
        _values.ContainsKey(settingKey) && !_inactive.Contains(settingKey);

    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
}
