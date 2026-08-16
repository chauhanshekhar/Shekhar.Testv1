using Trading.Infrastructure.Services;
using Trading.SignalEngine.Scoring.Ranking;
using Trading.SignalEngine.Scoring.Services;
using Trading.SignalEngine.Services;
using Trading.SignalEngine.Strategies;
using Trading.SignalEngine.Strategies.Expiry;

namespace Trading.Worker.Extensions;

internal static class SignalEngineExtensions
{
    internal static IServiceCollection AddSignalEngine(this IServiceCollection services)
    {
        services.AddScoped<EmaService>();
        services.AddScoped<RsiService>();
        services.AddScoped<AtrService>();
        services.AddScoped<AtrRiskService>();
        services.AddScoped<PcrService>();
        services.AddScoped<MarketRegimeService>();
        services.AddScoped<MarketClassificationService>();
        services.AddScoped<OiAnalysisService>();
        services.AddScoped<TrendScoreService>();
        services.AddScoped<OiScoreService>();
        services.AddScoped<VolumeScoreService>();
        services.AddScoped<GreeksScoreService>();
        services.AddScoped<LiquidityScoreService>();
        services.AddScoped<MomentumScoreService>();
        services.AddScoped<HeroZeroScoreService>();
        services.AddScoped<StrikeRankingService>();
        services.AddScoped<StrategySelectorService>();
        services.AddScoped<ExpiryMarketScoreService>();
        services.AddScoped<ExpiryScoreService>();
        services.AddScoped<ExpirySignalEngine>();
        services.AddScoped<SignalEngineService>();
        services.AddScoped<HeroZeroSignalEngine>();
        services.AddScoped<AdaptiveLotSizingService>();
        services.AddScoped<StrategyManager>();
        services.AddScoped<HistoricalCandleService>();
        services.AddScoped<PortfolioService>();
        services.AddScoped<PerformanceAnalyticsService>();
        services.AddScoped<PaperTradingService>();
        services.AddScoped<RiskManagementService>();
        services.AddScoped<BacktestService>();
        services.AddSingleton<EventCalendarService>();
        services.AddSingleton<AppErrorLogger>();
        services.AddSingleton<SystemSettingsService>();
        services.AddScoped<StrategyPerformanceService>();
        services.AddScoped<TradeCompletionService>();
        services.AddSingleton<StopLossOrderCoordinator>();
        return services;
    }
}
