using Trading.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Brokers.Groww;

// Token is stored in SystemSettings under key "GrowwAccessToken".
// Cache is held until 6:00 AM IST the next day (when Groww expires the token).
// Updating via UpdateAccessTokenAsync refreshes the cache immediately.
public class GrowwAuthService : IGrowwAuthService
{
    private const string TokenKey = "GrowwAccessToken";
    private const string ApiKeyKey = "GrowwApiKey";
    private const string TotpSecretKey = "GrowwTotpSecret";
    private const string ApiSecretKey = "GrowwApiSecret";
    private const string ModeKey  = "TradingMode";
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly IDbContextFactory<TradingDbContext> _dbFactory;
    private readonly IOptionsMonitor<GrowwSettings> _options;

    private string _cachedToken = "";
    private DateTime _cacheExpiry = DateTime.MinValue;   // UTC
    private readonly object _syncLock = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Mode cache — reloaded on every GetMode() call (lightweight string compare)
    private TradingMode? _cachedMode;

    public GrowwAuthService(
        IDbContextFactory<TradingDbContext> dbFactory,
        IOptionsMonitor<GrowwSettings> options)
    {
        _dbFactory = dbFactory;
        _options = options;
    }

    public string GetAccessToken()
    {
        if (IstClock.Now < _cacheExpiry && !string.IsNullOrWhiteSpace(_cachedToken))
            return _cachedToken;

        lock (_syncLock)
        {
            if (IstClock.Now < _cacheExpiry && !string.IsNullOrWhiteSpace(_cachedToken))
                return _cachedToken;

            using var db = _dbFactory.CreateDbContext();
            var row = db.SystemSettings.AsNoTracking()
                        .FirstOrDefault(s => s.Key == TokenKey);

            _cachedToken = row?.Value ?? _options.CurrentValue.AccessToken;
            _cacheExpiry = NextTokenExpiry();
        }

        return _cachedToken;
    }

    /// <summary>
    /// Credential lookup: SystemSettings row first, then appsettings/environment.
    ///
    /// Deliberately NOT cached. It is read only when a token is actually being generated — once a
    /// day plus retries, at most ~25 reads in 24 hours — so a per-call query costs nothing and
    /// guarantees a rotated credential takes effect immediately instead of after a restart.
    /// </summary>
    private string GetSetting(string key, string fallback)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var value = db.SystemSettings.AsNoTracking()
                          .FirstOrDefault(s => s.Key == key)?.Value;

            return !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }
        catch
        {
            // A database problem must not make the configured fallback unreachable.
            return fallback;
        }
    }

    public string GetApiKey() => GetSetting(ApiKeyKey, _options.CurrentValue.ApiKey);

    public string GetTotpSecret() => GetSetting(TotpSecretKey, _options.CurrentValue.TotpSecret);

    public string GetApiSecret() => GetSetting(ApiSecretKey, _options.CurrentValue.ApiSecret);

    public async Task UpdateAccessTokenAsync(string token, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var row = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == TokenKey, ct);
            if (row is null)
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = TokenKey,
                    Value = token,
                    UpdatedOn = IstClock.Now
                });
            }
            else
            {
                row.Value = token;
                row.UpdatedOn = IstClock.Now;
            }
            await db.SaveChangesAsync(ct);

            _cachedToken = token;
            _cacheExpiry = NextTokenExpiry();
        }
        finally
        {
            _lock.Release();
        }
    }

    public TradingMode GetMode()
    {
        using var db = _dbFactory.CreateDbContext();

        var row = db.SystemSettings
            .AsNoTracking()
            .FirstOrDefault(s => s.Key == ModeKey);

        if (row != null &&
            Enum.TryParse<TradingMode>(row.Value, true, out var mode))
        {
            return mode;
        }

        return _options.CurrentValue.Mode;
    }

    public void InvalidateModeCache() => _cachedMode = null;

    // Returns next 6:00 AM IST — when Groww expires the daily token.
    private static DateTime NextTokenExpiry()
    {
        var now = IstClock.Now;
        var expiry = now.Date.AddHours(6);   // 6:00 AM IST today
        if (now >= expiry) expiry = expiry.AddDays(1);
        return expiry;
    }
}
