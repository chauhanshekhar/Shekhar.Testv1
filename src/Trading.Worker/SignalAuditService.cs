using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

/// <summary>
/// Market Snapshot Audit bookkeeping for the signal pipeline — the IndicatorSnapshot row that
/// records what the market looked like this cycle and, when nothing fired, exactly what stopped
/// it. Extracted from SignalWorker unchanged; it is diagnostics only and is never read by trading
/// logic.
/// </summary>
public class SignalAuditService
{
    private readonly ILogger<SignalAuditService> _logger;
    private readonly AppErrorLogger _errorLogger;

    public SignalAuditService(ILogger<SignalAuditService> logger, AppErrorLogger errorLogger)
    {
        _logger = logger;
        _errorLogger = errorLogger;
    }

    // BlockedReason is nvarchar(500) — the combined CALL/PUT failure-condition list from
    // SignalGenerationResult can exceed that when almost every condition fails, so guard the
    // insert instead of letting SaveAsync's best-effort try/catch silently swallow it.
    public static string? Truncate(string? value, int maxLength) =>
        value != null && value.Length > maxLength ? value[..maxLength] : value;

    /// <summary>
    /// Candidate option leg at post-gate block time — the same lookup the placement path uses for
    /// a passing signal, just recorded instead of discarded. Missed-opportunity analysis over
    /// IndicatorSnapshot needs Delta/OI here since it isn't available anywhere else for blocked
    /// signals.
    /// </summary>
    public static void CaptureBlockedLeg(MarketSnapshot market, Signal signal, IndicatorSnapshot audit)
    {
        var entryLeg = market.OptionChain
            .FirstOrDefault(o => o.TradingSymbol == signal.OptionSymbol
                              || (o.StrikePrice == signal.StrikePrice && o.OptionType == signal.OptionType));

        audit.BlockedStrikePrice = signal.StrikePrice;
        audit.BlockedOptionType = signal.OptionType;
        audit.BlockedOptionSymbol = signal.OptionSymbol;
        audit.BlockedDelta = entryLeg?.Delta;
        audit.BlockedOptionLtp = entryLeg?.Ltp;
        audit.BlockedChangeInOi = entryLeg?.ChangeInOi;
    }

    // ── Best-effort, single insert, never blocks trading ─────────────────────────────────────
    // Failure is logged (not silently swallowed) but still never propagates — an audit-row insert
    // failure must not be able to take down signal generation.
    public async Task SaveAsync(IServiceProvider sp, IUnitOfWork uow, IndicatorSnapshot audit)
    {
        try
        {
            audit.CreatedOn = IstClock.Now;
            var repo = sp.GetRequiredService<IRepository<IndicatorSnapshot>>();
            await repo.StageAsync(audit);
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to save IndicatorSnapshot audit for {Symbol} at {SnapshotTime}",
                audit.Symbol, audit.SnapshotTime);
            await _errorLogger.LogAsync("SignalWorker.SaveAuditAsync", ex, audit.Symbol);
        }
    }
}
