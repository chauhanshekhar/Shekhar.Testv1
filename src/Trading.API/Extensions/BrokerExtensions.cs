using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
using Trading.API.Services;
using Trading.Infrastructure.Services;

namespace Trading.API.Extensions;

internal static class BrokerExtensions
{
    internal static IServiceCollection AddBroker(
        this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<TradingSettings>(cfg.GetSection("Trading"));
        services.Configure<GrowwSettings>(cfg.GetSection("Groww"));
        services.AddHttpClient();
        services.AddHttpClient("Groww");

        // Market data — always Groww
        services.AddSingleton<IGrowwAuthService, GrowwAuthService>();
        services.AddSingleton<IRuntimeSettingsService, RuntimeSettingsService>();
        services.AddSingleton<IGrowwHttpService>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Groww");
            GrowwHttpClientHelper.ConfigureClient(http, sp.GetRequiredService<IOptionsMonitor<GrowwSettings>>().CurrentValue);
            return new GrowwHttpService(
                http,
                sp.GetRequiredService<IGrowwAuthService>(),
                sp.GetRequiredService<AppErrorLogger>(),
                sp.GetRequiredService<IServiceScopeFactory>());
        });
        services.AddSingleton<IGrowwLiveDataService>(sp =>
            new GrowwLiveDataService(sp.GetRequiredService<IGrowwHttpService>()));
        services.AddSingleton<IMarketDataFeed, GrowwMarketFeed>();
        services.AddScoped<IHistoricalDataService>(sp =>
            new GrowwHistoricalDataService(sp.GetRequiredService<IGrowwHttpService>()));
        services.AddScoped<IGrowwUserService>(sp =>
            new GrowwUserService(sp.GetRequiredService<IGrowwHttpService>()));

        // Alert + notification
        services.AddSingleton<ITradingAlertService, TelegramAlertService>();
        services.AddSingleton<INotificationService, MarketHubService>();

        services.AddGrowwBroker();

        return services;
    }

    private static IServiceCollection AddGrowwBroker(this IServiceCollection services)
    {
        services.AddScoped<IMarginService>(sp =>
            new GrowwMarginService(
                sp.GetRequiredService<IGrowwHttpService>(),
                sp.GetRequiredService<IOptionsMonitor<GrowwSettings>>()));
        services.AddScoped<IGrowwPortfolioService>(sp =>
            new GrowwPortfolioService(sp.GetRequiredService<IGrowwHttpService>()));
        services.AddScoped<PaperOrderStrategy>();
        services.AddScoped<LiveOrderStrategy>(sp =>
            new LiveOrderStrategy(sp.GetRequiredService<IGrowwHttpService>(), sp.GetRequiredService<IInstrumentCache>()));
        services.AddScoped<HeroZeroOrderStrategy>(sp =>
            new HeroZeroOrderStrategy(sp.GetRequiredService<IGrowwHttpService>(), sp.GetRequiredService<IInstrumentCache>()));
        services.AddScoped<IOrderService, OrderRouter>();
        services.AddScoped<IGrowwSmartOrderService>(sp =>
            new GrowwSmartOrderService(sp.GetRequiredService<IGrowwHttpService>()));
        return services;
    }
}
