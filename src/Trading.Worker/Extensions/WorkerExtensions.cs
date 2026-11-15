namespace Trading.Worker.Extensions;

internal static class WorkerExtensions
{
    internal static IServiceCollection AddWorkers(this IServiceCollection services)
    {
        // Must run before anything that calls the broker — refreshes the daily access token.
        services.AddHostedService<GrowwTokenRefreshWorker>();
        services.AddHostedService<HolidayCalendarWorker>();   // loads holidays before market open
        services.AddHostedService<HistoricalDataWorker>();    // 1m+5m candles from Groww API
        services.AddHostedService<MarketFeedWorker>();
        services.AddHostedService<EmaWorker>();
        // Rolls TradingInstruments.Expiry forward; off unless StrategySettings EnableExpiryAutoRoll is set.
        services.AddSingleton<Trading.Infrastructure.Brokers.Groww.ExpiryRollService>();
        services.AddHostedService<ExpiryRollWorker>();        // before OptionChainWorker reads Expiry
        services.AddHostedService<OptionChainWorker>();
        services.AddHostedService<SignalWorker>();
        services.AddHostedService<ExitWorker>();
        services.AddHostedService<TelegramWorker>();
        services.AddHostedService<WhatsAppWorker>();
        services.AddHostedService<DailyPnlWorker>();
        services.AddHostedService<BrokerPositionSyncWorker>();
        services.AddHostedService<BrokerOrderSyncWorker>();
        services.AddHostedService<TickRetentionWorker>();
        // Analytics only — off unless TradingSettings.EnableOutcomeTracking is true.
        // Runs in its own scope and never blocks signal generation or order placement.
        services.AddHostedService<IndicatorSnapshotOutcomeWorker>();
        return services;
    }
}
