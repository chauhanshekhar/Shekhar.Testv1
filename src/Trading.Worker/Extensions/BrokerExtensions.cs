using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Services;

namespace Trading.Worker.Extensions;

internal static class BrokerExtensions
{
    internal static IServiceCollection AddBroker(
        this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<TradingSettings>(cfg.GetSection("Trading"));
        services.Configure<GrowwSettings>(cfg.GetSection("Groww"));
        services.Configure<MarketFeedSettings>(cfg.GetSection("MarketFeed"));
        services.Configure<HistoricalDataSettings>(cfg.GetSection("HistoricalData"));
        services.AddHttpClient();
        services.AddHttpClient("Groww");

        // Alert services
        services.AddSingleton<TelegramAlertService>();
        services.AddSingleton<WhatsAppAlertService>();
        services.AddSingleton<ITradingAlertService, CompositeAlertService>();
        services.AddSingleton<INotificationService, NullNotificationService>();

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

        services.AddGrowwBroker(cfg);

        return services;
    }

    private static IServiceCollection AddGrowwBroker(
        this IServiceCollection services, IConfiguration cfg)
    {
        services.AddScoped<IMarginService>(sp =>
            new GrowwMarginService(
                sp.GetRequiredService<IGrowwHttpService>(),
                sp.GetRequiredService<IOptionsMonitor<GrowwSettings>>()));
        services.AddScoped<IGrowwPortfolioService>(sp =>
            new GrowwPortfolioService(sp.GetRequiredService<IGrowwHttpService>()));
        services.AddSingleton<GrowwTokenService>();
        services.AddScoped<PaperOrderStrategy>();
        services.AddScoped<LiveOrderStrategy>(sp =>
            new LiveOrderStrategy(
                sp.GetRequiredService<IGrowwHttpService>(),
                sp.GetRequiredService<IInstrumentCache>(),
                sp.GetRequiredService<IStrategySettingsService>()));
        services.AddScoped<HeroZeroOrderStrategy>(sp =>
            new HeroZeroOrderStrategy(
                sp.GetRequiredService<IGrowwHttpService>(),
                sp.GetRequiredService<IInstrumentCache>(),
                sp.GetRequiredService<IStrategySettingsService>()));
        services.AddScoped<IOrderService, OrderRouter>();
        services.AddScoped<IGrowwSmartOrderService>(sp =>
            new GrowwSmartOrderService(sp.GetRequiredService<IGrowwHttpService>()));
        return services;
    }
}
