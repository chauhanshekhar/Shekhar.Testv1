using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Singleton cache over StrategySettings. Loaded once at startup (Program.cs calls ReloadAsync,
/// same convention as IInstrumentCache) and refreshed every 5 minutes by an internal timer —
/// the Signal Engine never queries SQL Server directly. A failed refresh logs and keeps serving
/// the previous cache; reads always hit the in-memory snapshot, never the database.
///
/// IsActive SEMANTICS
/// ------------------
/// IsActive = 0 on a BOOLEAN key means the feature is OFF. It does NOT mean "ignore this row".
///
/// Previously inactive rows were filtered out of the cache entirely, so the caller fell back to
/// its COMPILED DEFAULT — and for every Enable* flag that default is `true`. Deactivating
/// EnableCandleFilter therefore turned the candle filter ON, the exact opposite of the intent.
/// That misfired in production on 2026-09-10: ten filters were deactivated to widen entry and
/// every one of them stayed enforced, which the blocked-reason log showed by name.
///
/// NUMERIC keys keep the old behaviour: IsActive = 0 falls back to the caller's default. A
/// threshold has no natural "off" value — 0 is permissive for a minimum and prohibitive for a
/// maximum — so the compiled default is the only safe reading. To relax a numeric limit, change
/// SettingValue; to switch a feature off, clear IsActive (or set SettingValue = 0, still valid).
///
/// A row that exists for a SPECIFIC symbol and is inactive turns the feature off FOR THAT SYMBOL;
/// it does not fall through to the global row.
///
/// NO ROW AT ALL vs. an inactive row are different questions with different answers:
///   - GetDecimal/GetBool/GetInt/GetString: no row -> caller's compiled default (unchanged).
///     This is what a value-only consumer (e.g. ComputeConfidence's BodyPctMinimum) reads.
///   - IsSettingActive: no row -> NOT active (changed - see its own doc comment). This is what a
///     CONDITION reads to decide whether it applies at all. An unconfigured symbol therefore does
///     not silently enforce every threshold's hardcoded number; the condition is treated the same
///     as an explicitly deactivated one, until someone actually configures it for that symbol.
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

    /// <summary>One resolved row: the stored value plus whether the row is switched on.</summary>
    internal readonly record struct SettingRow(decimal Value, bool IsActive);

    private volatile Dictionary<string, Dictionary<string, SettingRow>> _cache =
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
        // An inactive numeric row falls back to the caller's default: 0 is not a meaningful
        // "off" for a threshold. See the IsActive note on the class.
        if (!TryResolve(settingKey, symbol, out var row) || !row.IsActive)
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        return row.Value;
    }

    public int GetInt(string settingKey, string? symbol, int defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var row) || !row.IsActive)
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        try
        {
            return (int)row.Value;
        }
        catch (OverflowException)
        {
            WarnOnce(settingKey, $"value {row.Value} does not fit in Int32 — using default {defaultValue}");
            return defaultValue;
        }
    }

    public bool GetBool(string settingKey, string? symbol, bool defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var row))
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        // THE SWITCH. A boolean row that exists but is inactive means the feature is OFF - it does
        // NOT fall back to the compiled default, which for every Enable* flag is `true`.
        if (!row.IsActive)
            return false;

        return row.Value != 0m;
    }

    public string GetString(string settingKey, string? symbol, string defaultValue)
    {
        if (!TryResolve(settingKey, symbol, out var row) || !row.IsActive)
        {
            WarnOnce(settingKey, $"not configured for Symbol={symbol ?? "(global)"} — using default {defaultValue}");
            return defaultValue;
        }

        return row.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Builds the lookup from raw rows. Split out so the IsActive semantics can be tested without
    /// a database or an in-memory EF provider — the getters are pure once the cache is populated.
    /// </summary>
    internal static Dictionary<string, Dictionary<string, SettingRow>> BuildCache(
        IEnumerable<StrategySetting> rows)
    {
        var next = new Dictionary<string, Dictionary<string, SettingRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!next.TryGetValue(row.SettingKey, out var bySymbol))
            {
                bySymbol = new Dictionary<string, SettingRow>(StringComparer.OrdinalIgnoreCase);
                next[row.SettingKey] = bySymbol;
            }

            bySymbol[row.Symbol ?? GlobalSymbolKey] = new SettingRow(row.SettingValue, row.IsActive);
        }

        return next;
    }

    /// <summary>Test seam: install a prebuilt cache without touching the database.</summary>
    internal void SetCacheForTest(Dictionary<string, Dictionary<string, SettingRow>> cache) =>
        _cache = cache;

    /// <summary>
    /// Exact-symbol row wins over the global row, as before. The row is returned WITH its IsActive
    /// flag, so an inactive symbol-specific row switches the feature off for that symbol rather
    /// than falling through to the global row.
    ///
    /// NO ROW AT ALL -> NOT active. This deliberately does NOT mean "the compiled default
    /// applies, so the condition still enforces". A condition that has never been configured for
    /// a symbol must not silently enforce whatever number happens to be hardcoded in the calling
    /// code — it has no evidence behind it for that symbol. GetDecimal/GetBool still return their
    /// caller-supplied default when no row exists (that part is unchanged, and used on its own
    /// for confidence-only inputs like BodyPctMinimum); it is only the paired
    /// IsSettingActive(...) check — the one that gates whether a CONDITION applies at all — that
    /// now reads "unconfigured" as "off", the same as an explicitly deactivated row.
    /// </summary>
    public bool IsSettingActive(string settingKey, string? symbol)
    {
        if (!TryResolve(settingKey, symbol, out var row))
            return false;

        return row.IsActive;
    }

    private bool TryResolve(string settingKey, string? symbol, out SettingRow row)
    {
        row = default;
        var snapshot = _cache;

        if (!snapshot.TryGetValue(settingKey, out var bySymbol))
            return false;

        if (symbol is not null && bySymbol.TryGetValue(symbol, out var exact))
        {
            row = exact;
            return true;
        }

        if (bySymbol.TryGetValue(GlobalSymbolKey, out var global))
        {
            row = global;
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
            // INACTIVE ROWS ARE LOADED TOO. Filtering them out here is what made IsActive = 0
            // silently restore the compiled default; the flag now travels with the value so the
            // getters can honour it.
            var rows = await db.StrategySettings
                .AsNoTracking()
                .ToListAsync(ct);

            _cache = BuildCache(rows);
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
