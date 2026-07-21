using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.API.Services;

namespace Trading.API.Extensions;

internal static class SignalEngineExtensions
{
    internal static IServiceCollection AddSignalEngine(this IServiceCollection services)
    {
        services.AddSingleton<SystemSettingsService>();
        services.AddScoped<JwtService>();
        services.AddScoped<EmaService>();
        services.AddScoped<RsiService>();
        services.AddScoped<AtrService>();
        services.AddScoped<AtrRiskService>();
        services.AddScoped<OptionTargetService>();
        services.AddScoped<BacktestService>();
        services.AddScoped<PaperTradingService>();
        services.AddScoped<RiskManagementService>();
        services.AddScoped<PortfolioService>();
        services.AddScoped<PerformanceAnalyticsService>();
        services.AddScoped<HistoricalCandleService>();
        services.AddScoped<SignalEngineService>();
        services.AddScoped<OiAnalysisService>();
        services.AddScoped<PcrService>();
        services.AddSingleton<EventCalendarService>();
        services.AddSingleton<AppErrorLogger>();
        return services;
    }
}
