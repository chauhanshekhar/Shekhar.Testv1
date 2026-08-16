using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Singleton cache over StrategySettings. Loaded once at startup (Program.cs calls ReloadAsync,
/// same convention as IInstrumentCache) and refreshed every 5 minutes by an internal timer —
/// the Signal Engine never queries SQL Server directly. A failed refresh logs and keeps serving
/// the previous cache; reads always hit the in-memory snapshot, never the database.
/// </summary>
public class StrategySettingsService : IStrategySettingsService, IDisposable
{
    // Sentinel for Symbol == null (global/default row) inside the per-key dictionary.
    private const string GlobalSymbolKey = "";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<TradingDbContext> _dbFactory;
    private readonly ILogger<StrategySettingsService> _logger;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly Timer _refreshTimer;

    // Deduped so a missing/invalid setting warns once, not on every signal that reads it.
    private readonly ConcurrentDictionary<string, byte> _warnedKeys = new(StringComparer.OrdinalIgnoreCase);

    private volatile Dictionary<string, Dictionary<string, decimal>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public StrategySettingsService(
        IDbContextFactory<TradingDbContext> dbFactory,
        ILogger<StrategySettingsService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _refreshTimer = new Timer(_ => _ = ReloadAsync(), null, RefreshInterval, RefreshInterval);
    }

    public decimal GetDecimal(string settingKey, string? symbol, decimal defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var value))
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        return value;
    }

    public int GetInt(string settingKey, string? symbol, int defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var value))
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        try
        {
            return (int)value;
        }
        catch (OverflowException)
        {
            WarnOnce(settingKey, $"value {value} does not fit in Int32 — using default {defaultValue}");
            return defaultValue;
        }
    }

    public bool GetBool(string settingKey, string? symbol, bool defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var value))
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        return value != 0m;
    }

    public string GetString(string settingKey, string? symbol, string defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var value))
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool TryResolve(string settingKey, string? symbol, out decimal value)
    {
        value = default;
        var snapshot = _cache;

        if (!snapshot.TryGetValue(settingKey, out var bySymbol))
            return false;

        if (symbol is not null && bySymbol.TryGetValue(symbol, out var exact))
        {
            value = exact;
            return true;
        }

        if (bySymbol.TryGetValue(GlobalSymbolKey, out var global))
        {
            value = global;
            return true;
        }

        return false;
    }

    private void WarnOnce(string settingKey, string message)
    {
        _warnedKeys.TryAdd(settingKey, 0);
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (!await _reloadGate.WaitAsync(0, ct))
            return; // a reload is already in flight — skip rather than pile up

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var rows = await db.StrategySettings
                .Where(s => s.IsActive)
                .AsNoTracking()
                .ToListAsync(ct);

            var next = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (!next.TryGetValue(row.SettingKey, out var bySymbol))
                {
                    bySymbol = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    next[row.SettingKey] = bySymbol;
                }

                bySymbol[row.Symbol ?? GlobalSymbolKey] = row.SettingValue;
            }

            _cache = next;
            _warnedKeys.Clear(); // give previously-missing keys a chance to warn again if still absent
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "StrategySettingsService cache refresh failed — continuing with previous cache ({KeyCount} key(s))",
                _cache.Count);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public void Dispose() => _refreshTimer.Dispose();
}
