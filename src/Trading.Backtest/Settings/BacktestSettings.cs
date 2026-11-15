using Trading.Application.Interfaces;
using Trading.Domain.Entities;

namespace Trading.Backtest.Settings;

/// <summary>
/// In-memory <see cref="IStrategySettingsService"/> for replays. It copies the read semantics of the live
/// StrategySettingsService exactly (see that class for the reasoning), so a configuration that deactivates a row
/// behaves in the backtest as it would in production:
///   - exact-symbol row wins over the global row, and an inactive symbol row does NOT fall through to the global one;
///   - GetBool: no row = caller default; inactive row = false;
///   - GetDecimal / GetInt / GetString: no row or inactive row = caller default;
///   - IsSettingActive: no row = NOT active.
/// Instances are mutable only through the fluent Set / Remove / Deactivate methods; <see cref="Clone"/> gives an
/// independent copy so one named configuration never leaks into another.
/// </summary>
public sealed class BacktestSettings : IStrategySettingsService
{
    private const string Global = "";

    private readonly Dictionary<string, Dictionary<string, (decimal Value, bool Active)>> _rows =
        new(StringComparer.OrdinalIgnoreCase);

    public static BacktestSettings FromRows(IEnumerable<StrategySetting> rows)
    {
        var s = new BacktestSettings();
        foreach (var r in rows)
            s.Put(r.SettingKey, r.Symbol, r.SettingValue, r.IsActive);
        return s;
    }

    public BacktestSettings Clone()
    {
        var c = new BacktestSettings();
        foreach (var (key, bySymbol) in _rows)
            c._rows[key] = new Dictionary<string, (decimal, bool)>(bySymbol, StringComparer.OrdinalIgnoreCase);
        return c;
    }

    /// <summary>Creates or replaces a row (active by default). Symbol null = the global row.</summary>
    public BacktestSettings Set(string key, string? symbol, decimal value, bool active = true)
    {
        Put(key, symbol, value, active);
        return this;
    }

    /// <summary>Removes a row entirely, i.e. "no row exists" (IsSettingActive then reads false).</summary>
    public BacktestSettings Remove(string key, string? symbol)
    {
        if (_rows.TryGetValue(key, out var bySymbol))
            bySymbol.Remove(symbol ?? Global);
        return this;
    }

    /// <summary>Keeps the value but switches the row off (IsActive = 0).</summary>
    public BacktestSettings Deactivate(string key, string? symbol)
    {
        var (found, row) = Find(key, symbol, exactOnly: true);
        Put(key, symbol, found ? row.Value : 0m, active: false);
        return this;
    }

    public decimal GetDecimal(string settingKey, string? symbol, decimal defaultValue) =>
        TryResolve(settingKey, symbol, out var row) && row.Active ? row.Value : defaultValue;

    public int GetInt(string settingKey, string? symbol, int defaultValue) =>
        TryResolve(settingKey, symbol, out var row) && row.Active && row.Value is >= int.MinValue and <= int.MaxValue
            ? (int)row.Value
            : defaultValue;

    public bool GetBool(string settingKey, string? symbol, bool defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var row))
            return defaultValue;
        return row.Active && row.Value != 0m;
    }

    public string GetString(string settingKey, string? symbol, string defaultValue) =>
        TryResolve(settingKey, symbol, out var row) && row.Active
            ? row.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : defaultValue;

    public bool IsSettingActive(string settingKey, string? symbol) =>
        TryResolve(settingKey, symbol, out var row) && row.Active;

    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;

    private void Put(string key, string? symbol, decimal value, bool active)
    {
        if (!_rows.TryGetValue(key, out var bySymbol))
            _rows[key] = bySymbol = new Dictionary<string, (decimal, bool)>(StringComparer.OrdinalIgnoreCase);
        bySymbol[symbol ?? Global] = (value, active);
    }

    private (bool Found, (decimal Value, bool Active) Row) Find(string key, string? symbol, bool exactOnly)
    {
        if (_rows.TryGetValue(key, out var bySymbol)
            && bySymbol.TryGetValue(symbol ?? Global, out var exact))
            return (true, exact);
        return (false, default);
    }

    private bool TryResolve(string key, string? symbol, out (decimal Value, bool Active) row)
    {
        row = default;
        if (!_rows.TryGetValue(key, out var bySymbol))
            return false;
        if (symbol is not null && bySymbol.TryGetValue(symbol, out var exact))
        {
            row = exact;
            return true;
        }
        if (bySymbol.TryGetValue(Global, out var global))
        {
            row = global;
            return true;
        }
        return false;
    }
}
