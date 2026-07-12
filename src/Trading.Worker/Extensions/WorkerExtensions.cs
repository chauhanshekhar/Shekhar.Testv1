namespace Trading.Worker.Extensions;

internal static class WorkerExtensions
{
    internal static IServiceCollection AddWorkers(this IServiceCollection services)
    {
        services.AddHostedService<HolidayCalendarWorker>();   // loads holidays before market open
        services.AddHostedService<HistoricalDataWorker>();    // 1m+5m candles from Groww API
        services.AddHostedService<MarketFeedWorker>();
        services.AddHostedService<EmaWorker>();
        services.AddHostedService<OptionChainWorker>();
        services.AddHostedService<SignalWorker>();
        services.AddHostedService<ExitWorker>();
        services.AddHostedService<TelegramWorker>();
        services.AddHostedService<WhatsAppWorker>();
        services.AddHostedService<DailyPnlWorker>();
        services.AddHostedService<BrokerPositionSyncWorker>();
        services.AddHostedService<BrokerOrderSyncWorker>();
        services.AddHostedService<TickRetentionWorker>();
        return services;
    }
}
