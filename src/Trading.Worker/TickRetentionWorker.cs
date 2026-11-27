using Trading.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.Worker;

/// <summary>
/// Runs once per day after market close and enforces the data-retention policy:
///   - MarketTick rows: keep 7 days (raw tick data grows fast)
///   - 1m Candle rows:  keep 6 months
///   - 5m+ Candles:     keep forever
///   - OptionChainData: keep 90 days (OI analysis, PCR trend, backtesting, SignalOutcome correlation)
///   - OptionMinuteHistory: keep 90 days (per-minute premium/delta history for backtests)
///   - SignalOptionTicks: keep 90 days (per-second premium path ExitWorker acted on, for exit replay checks)
/// Uses ExecuteDeleteAsync (single SQL DELETE … WHERE — no rows loaded into memory).
/// </summary>
public class TickRetentionWorker : BackgroundService
{
    private readonly ILogger<TickRetentionWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public TickRetentionWorker(
        ILogger<TickRetentionWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for initial startup to settle.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TickRetentionWorker error");
            }

            // Sleep until 23:30 IST (30 minutes after market close)
            var delay = TimeUntilNextRun();
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunRetentionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var tickRepo = scope.ServiceProvider.GetRequiredService<IRepository<MarketTick>>();
        var candleRepo = scope.ServiceProvider.GetRequiredService<IRepository<Candle>>();
        var optionRepo = scope.ServiceProvider.GetRequiredService<IRepository<OptionChainData>>();
        var optionHistoryRepo = scope.ServiceProvider.GetRequiredService<IRepository<OptionMinuteHistory>>();

        var tickCutoff = IstClock.Now.AddDays(-7);
        var candle1mCutoff = IstClock.Now.AddDays(-180);   // 6 months
        var optionCutoff = IstClock.Now.AddDays(-90);

        await tickRepo.ExecuteDeleteAsync(t => t.TickTime < tickCutoff);

        await candleRepo.ExecuteDeleteAsync(
            c => c.Symbol.EndsWith("_1m") && c.CandleTime < candle1mCutoff);

        await optionRepo.ExecuteDeleteAsync(o => o.CreatedOn < optionCutoff);

        await optionHistoryRepo.ExecuteDeleteAsync(o => o.MinuteTime < optionCutoff);

        var signalTickRepo = scope.ServiceProvider.GetRequiredService<IRepository<SignalOptionTick>>();
        await signalTickRepo.ExecuteDeleteAsync(t => t.TickTime < optionCutoff);
    }

    private static TimeSpan TimeUntilNextRun()
    {
        var nowIst = TradingConstants.NowIst();
        var target = nowIst.Date.AddHours(23).AddMinutes(30);   // 23:30 IST
        if (nowIst >= target) target = target.AddDays(1);
        return target - nowIst;
    }
}
