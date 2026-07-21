using Microsoft.EntityFrameworkCore;
using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Infrastructure.Data;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.API.Extensions;

internal static class DatabaseExtensions
{
    internal static IServiceCollection AddDatabase(
        this IServiceCollection services, IConfiguration cfg)
    {
        // AddDbContextFactory registers both the factory (Scoped) and a scoped DbContext.
        // Do NOT also call AddDbContext — it conflicts with Singleton consumers of IDbContextFactory.
        services.AddDbContextFactory<TradingDbContext>(o =>
            o.UseSqlServer(cfg.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IInstrumentCache, InstrumentCache>();
        services.AddSingleton<IStrategySettingsService, StrategySettingsService>();

        services.AddHealthChecks()
            .AddDbContextCheck<TradingDbContext>("database");

        return services;
    }
}
