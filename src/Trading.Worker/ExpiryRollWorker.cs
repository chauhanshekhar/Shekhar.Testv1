using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.Worker;

/// <summary>
/// Keeps TradingInstruments.Expiry current so nobody sets expiry dates by hand.
///
/// OptionChainWorker NULLs an instrument's Expiry once the date has passed and then skips that
/// instrument; IsExpiryDay() is also false for it. Without a new date the index silently loses its
/// option chain and expiry-day routing. This worker supplies the next date from Groww's instrument CSV
/// (ExpiryRollService).
///
/// SAFE BY DEFAULT: does nothing unless StrategySettings 'EnableExpiryAutoRoll' is active and
/// non-zero (global row, Symbol NULL). Flag is re-read on every run, so it can be switched off live.
///
/// RULES
///   * Only a missing / unparseable / PAST date is replaced. A date that is today or later is never
///     touched, so expiry day itself keeps IsExpiryDay true until the day has passed.
///   * A candidate more than ExpiryRollService.MaxPlausibleDaysAhead days out is not applied.
///   * No CSV match, a download failure or an unknown layout leaves the stored value unchanged and
///     raises a CRITICAL AppErrors entry. A date is never guessed.
///   * Touches only TradingInstruments.Expiry/UpdatedOn — no order, exit or risk logic.
///
/// InstrumentCache is loaded once at startup, so it is reloaded after a change; otherwise the new
/// date would not be seen until the next restart.
/// </summary>
public class ExpiryRollWorker : BackgroundService
{
    internal const string EnableKey = "EnableExpiryAutoRoll";

    /// <summary>Before market open, so the new date is in place for the session.</summary>
    private static readonly TimeSpan RunAt = new(8, 30, 0);

    private readonly ILogger<ExpiryRollWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentCache _instrumentCache;
    private readonly IStrategySettingsService _settings;
    private readonly ExpiryRollService _expiryService;
    private readonly AppErrorLogger _errorLogger;

    public ExpiryRollWorker(
        ILogger<ExpiryRollWorker> logger,
        IServiceScopeFactory scopeFactory,
        IInstrumentCache instrumentCache,
        IStrategySettingsService settings,
        ExpiryRollService expiryService,
        AppErrorLogger errorLogger)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _instrumentCache = instrumentCache;
        _settings = settings;
        _expiryService = expiryService;
        _errorLogger = errorLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup run: a container that comes up after the old date has passed must not wait a day.
        await RunOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeUntilNextRun(), stoppingToken); }
            catch (OperationCanceledException) { break; }

            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            if (!_settings.GetBool(EnableKey, null, false))
            {
                _logger.LogInformation("Expiry auto-roll is OFF ({Key} not enabled)", EnableKey);
                return;
            }

            var today = IstClock.Now.Date;

            var stale = _instrumentCache.GetActive()
                .Where(i => ExpiryRollService.NeedsRoll(i.Expiry, today))
                .Select(i => (i.Symbol, i.Exchange))
                .ToList();

            if (stale.Count == 0)
                return;

            var nearest = await _expiryService.GetNearestExpiriesAsync(stale, today, ct);
            var changed = false;

            foreach (var (symbol, _) in stale)
            {
                if (!nearest.TryGetValue(symbol, out var candidate))
                {
                    await ReportAsync(symbol, "No upcoming option expiry found in Groww instrument CSV; Expiry left unchanged");
                    continue;
                }

                if (!ExpiryRollService.IsPlausible(candidate, today))
                {
                    await ReportAsync(symbol,
                        $"Nearest expiry {candidate} is more than {ExpiryRollService.MaxPlausibleDaysAhead} days out; Expiry left unchanged");
                    continue;
                }

                changed |= await ApplyAsync(symbol, candidate, today, ct);
            }

            if (changed)
                await _instrumentCache.ReloadAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            // Must never crash the worker host.
            _logger.LogError(ex, "Expiry auto-roll run failed");
            await _errorLogger.LogAsync("ExpiryRollWorker", ex, severity: "CRITICAL");
        }
    }

    /// <summary>Re-reads the DB row first, so a date you set by hand since the cache loaded is never overwritten.</summary>
    private async Task<bool> ApplyAsync(string symbol, string candidate, DateTime today, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<TradingInstrument>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var row = await repo.FirstOrDefaultAsync(i => i.Symbol == symbol);
        if (row is null || !ExpiryRollService.NeedsRoll(row.Expiry, today))
            return false;

        var old = row.Expiry;
        row.Expiry = candidate;
        row.UpdatedOn = IstClock.Now;
        await uow.SaveChangesAsync();

        _logger.LogInformation("Expiry auto-roll: {Symbol} Expiry {Old} -> {New}", symbol, old ?? "(none)", candidate);
        return true;
    }

    private async Task ReportAsync(string symbol, string message)
    {
        _logger.LogError("Expiry auto-roll: {Symbol}: {Message}", symbol, message);
        await _errorLogger.LogAsync("ExpiryRollWorker", new InvalidOperationException(message),
            symbol: symbol, severity: "CRITICAL");
    }

    /// <summary>Time until the next 08:30 IST.</summary>
    private static TimeSpan TimeUntilNextRun()
    {
        var now = IstClock.Now;
        var next = now.Date.Add(RunAt);
        if (now >= next) next = next.AddDays(1);
        return next - now;
    }
}
