using Microsoft.Extensions.Options;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Provides the current TradingSettings. Loaded purely from appsettings.json (via
/// IOptionsMonitor, so edits take effect without a restart) — trading strategy behavior
/// must not depend on DB availability. No DB round-trip, no caching needed.
/// </summary>
public class SystemSettingsService
{
    private readonly IOptionsMonitor<TradingSettings> _options;

    public SystemSettingsService(IOptionsMonitor<TradingSettings> options)
    {
        _options = options;
    }

    public Task<TradingSettings> GetAsync(CancellationToken ct = default)
        => Task.FromResult(_options.CurrentValue);
}
