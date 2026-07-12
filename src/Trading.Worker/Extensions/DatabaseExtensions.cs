using Microsoft.EntityFrameworkCore;
using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Infrastructure.Data;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.Worker.Extensions;

internal static class DatabaseExtensions
{
    internal static IServiceCollection AddDatabase(
        this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContextFactory<TradingDbContext>(o =>
            o.UseSqlServer(cfg.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IInstrumentCache, InstrumentCache>();

        return services;
    }
}
