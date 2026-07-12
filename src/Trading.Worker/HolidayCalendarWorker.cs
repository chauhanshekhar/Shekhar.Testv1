using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.Worker;

/// <summary>
/// Loads exchange holidays from DB into HolidayCache at startup,
/// then refreshes at midnight IST every day.
/// No broker calls — purely a DB read.
/// </summary>
public class HolidayCalendarWorker : BackgroundService
{
    private readonly ILogger<HolidayCalendarWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public HolidayCalendarWorker(
        ILogger<HolidayCalendarWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger       = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Load immediately on startup
        await LoadHolidaysAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            // Sleep until next IST midnight, then reload
            var delay = NextMidnightDelay();
            _logger.LogDebug("HolidayCalendarWorker: next refresh in {Minutes:N0} min", delay.TotalMinutes);
            await Task.Delay(delay, stoppingToken);
            await LoadHolidaysAsync();
        }
    }

    private async Task LoadHolidaysAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<ExchangeHoliday>>();

            var holidays = await repo.FindAsync(h => h.IsActive);
            HolidayCache.Set(holidays.Select(h => h.HolidayDate));

            _logger.LogInformation(
                "HolidayCalendarWorker: loaded {Count} exchange holidays into cache", holidays.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HolidayCalendarWorker: failed to load holidays — MarketHours will not block holidays today");
        }
    }

    private static TimeSpan NextMidnightDelay()
    {
        var now = IstClock.Now;
        var nextMidnight = now.Date.AddDays(1); // tomorrow 00:00 IST
        return nextMidnight - now;
    }
}
