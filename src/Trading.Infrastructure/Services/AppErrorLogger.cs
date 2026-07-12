using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Services;

public class AppErrorLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppErrorLogger> _logger;

    public AppErrorLogger(IServiceScopeFactory scopeFactory, ILogger<AppErrorLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogAsync(
        string source,
        Exception ex,
        string? symbol = null,
        string? context = null,
        string severity = "ERROR",
        string? requestUrl = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
            await db.AppErrors.AddAsync(new AppError
            {
                Source       = source,
                Message      = ex.Message,
                StackTrace   = ex.StackTrace,
                InnerMessage = ex.InnerException?.Message,
                Symbol       = symbol,
                Context      = context,
                RequestUrl   = requestUrl,
                Severity     = severity,
                OccurredOn   = IstClock.Now
            });
            await db.SaveChangesAsync();
        }
        catch (Exception dbEx)
        {
            _logger.LogCritical(dbEx, "AppErrorLogger failed to persist error from {Source}", source);
        }
    }

    public Task LogWarningAsync(string source, Exception ex, string? symbol = null, string? context = null, string? requestUrl = null)
        => LogAsync(source, ex, symbol, context, "WARNING", requestUrl);

    public Task LogCriticalAsync(string source, Exception ex, string? symbol = null, string? context = null, string? requestUrl = null)
        => LogAsync(source, ex, symbol, context, "CRITICAL", requestUrl);
}
