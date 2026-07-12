using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure;

public interface IInstrumentCache
{
    IReadOnlyList<TradingInstrument> GetActive();
    Task ReloadAsync(CancellationToken ct = default);
}

public static class InstrumentCacheExtensions
{
    public static int GetLotSize(this IInstrumentCache cache, string symbol)
        => cache.GetActive().FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))?.LotSize ?? 1;

    public static int GetStrikeInterval(this IInstrumentCache cache, string symbol)
        => cache.GetActive().FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))?.StrikeInterval ?? 50;

    public static int GetIndexStopPoints(this IInstrumentCache cache, string symbol)
        => cache.GetActive().FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))?.IndexStopPoints ?? 50;

    public static string GetExchange(this IInstrumentCache cache, string symbol)
        => cache.GetActive().FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))?.Exchange ?? "NSE";

    public static bool IsExpiryDay(this IInstrumentCache cache, string symbol)
    {
        var expiry = cache.GetActive()
            .FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))?.Expiry;

        if (string.IsNullOrWhiteSpace(expiry)) return false;

        return DateTime.TryParseExact(expiry, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var expiryDate)
            && expiryDate.Date == IstClock.Now.Date;
    }
}

public class InstrumentCache : IInstrumentCache
{
    private readonly IDbContextFactory<TradingDbContext> _dbFactory;
    private readonly ILogger<InstrumentCache> _logger;
    private volatile IReadOnlyList<TradingInstrument> _cache = [];

    public InstrumentCache(
        IDbContextFactory<TradingDbContext> dbFactory,
        ILogger<InstrumentCache> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public IReadOnlyList<TradingInstrument> GetActive() => _cache;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        _cache = await db.TradingInstruments
            .Where(i => i.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);

        _logger.LogInformation(
            "InstrumentCache loaded {Count} active instrument(s): {Symbols}",
            _cache.Count,
            string.Join(", ", _cache.Select(i => i.Symbol)));
    }
}
