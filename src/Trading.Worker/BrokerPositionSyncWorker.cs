using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.Worker;

/// <summary>
/// Every 30 seconds reconciles DB signals against broker positions.
///
/// Catches gaps caused by:
///   - DB save failure after broker order accepted
///   - App restart while a position was open
///   - Broker partial fill not reflected in DB
///
/// Actions:
///   - PENDING signals with no broker position → mark ORDER_REJECTED (order never reached broker)
///   - ORDER_PLACED signals with no broker position → alert (position closed outside app)
///   - Broker positions with no matching DB signal → alert (orphan position)
/// </summary>
public class BrokerPositionSyncWorker : BackgroundService
{
    private readonly ILogger<BrokerPositionSyncWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradingAlertService _alerts;
    private readonly AppErrorLogger _errorLogger;

    // Only reconcile signals created in this window — avoids scanning full history
    private static readonly TimeSpan ReconcileWindow = TimeSpan.FromHours(8);

    public BrokerPositionSyncWorker(
        ILogger<BrokerPositionSyncWorker> logger,
        IServiceScopeFactory scopeFactory,
        ITradingAlertService alerts,
        AppErrorLogger errorLogger)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _alerts = alerts;
        _errorLogger = errorLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BrokerPositionSyncWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (MarketHours.IsOpen())
                    await ReconcileAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BrokerPositionSyncWorker error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ReconcileAsync()
    {
        using var scope  = _scopeFactory.CreateScope();
        var sp           = scope.ServiceProvider;
        var signalRepo   = sp.GetRequiredService<IRepository<Signal>>();
        var positionRepo = sp.GetRequiredService<IRepository<Position>>();
        var portfolio    = sp.GetRequiredService<IGrowwPortfolioService>();
        var uow          = sp.GetRequiredService<IUnitOfWork>();
        var instruments  = sp.GetRequiredService<IInstrumentCache>();

        // Load open DB signals from last 8 hours
        var windowStart = IstClock.Now - ReconcileWindow;
        var dbSignals = await signalRepo.FindAsync(
 s => s.CreatedOn >= windowStart
 &&
 (
     s.Status == SignalStatus.Pending ||
     s.Status == SignalStatus.OrderPlaced ||
     s.Status == SignalStatus.ExitPending ||
     s.Status == SignalStatus.PartialExitPending
 ));
        if (dbSignals.Count == 0) return;

        // Fetch live broker positions
        List<Application.Models.PositionEntry> brokerPositions;
        try
        {
            brokerPositions = await portfolio.GetPositionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BrokerPositionSyncWorker: could not fetch broker positions — skipping reconcile");
            return;
        }

        var brokerSymbols = brokerPositions
            .Where(p => p.Quantity != 0)
            .Select(p => p.TradingSymbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool anyChange = false;

        foreach (var signal in dbSignals)
        {
            var hasBrokerPosition = !string.IsNullOrEmpty(signal.OptionSymbol)
                && brokerSymbols.Contains(signal.OptionSymbol);

            if (signal.Status == SignalStatus.Pending)
            {
                // PENDING + no broker position = order never sent (crash between save and PlaceOrderAsync)
                if (!hasBrokerPosition)
                {
                    _logger.LogWarning(
                        "BrokerSync: signal {Id} {Symbol} stuck PENDING with no broker position — marking ORDER_REJECTED",
                        signal.Id, signal.Symbol);
                    signal.Status = SignalStatus.OrderRejected;
                    anyChange = true;
                }
            }
            else if (signal.Status == SignalStatus.OrderPlaced)
            {
                if (!hasBrokerPosition)
                {
                    // ORDER_PLACED + no broker position = position closed outside app
                    _logger.LogWarning(
                        "BrokerSync: signal {Id} {Symbol} ORDER_PLACED but no broker position found — position may have been closed externally",
                        signal.Id, signal.Symbol);
                    await _alerts.SendCriticalAsync(
                        $"BrokerSync: {signal.Symbol} signal #{signal.Id} has no broker position. " +
                        $"Position may have been closed outside the app. Check broker dashboard.");
                }
                else
                {
                    // ── Quantity mismatch check ───────────────────────────────
                    // DB signal.Quantity is in lots; broker Quantity is in contracts.
                    // Get the broker position to compare actual filled contracts vs expected.
                    var brokerPos = brokerPositions
                        .FirstOrDefault(p => p.TradingSymbol.Equals(
                            signal.OptionSymbol, StringComparison.OrdinalIgnoreCase));

                    if (brokerPos is not null)
                    {
                        var expectedContracts = signal.RemainingQty * instruments.GetLotSize(signal.Symbol);
                        var actualContracts   = Math.Abs(brokerPos.Quantity ?? 0);

                        if (expectedContracts > 0 && actualContracts != expectedContracts)
                        {
                            _logger.LogWarning(
                                "BrokerSync: signal {Id} {Symbol} qty mismatch — DB={Expected} contracts, Broker={Actual} contracts",
                                signal.Id, signal.Symbol, expectedContracts, actualContracts);
                            await _alerts.SendCriticalAsync(
                                $"BrokerSync: {signal.Symbol} signal #{signal.Id} QUANTITY MISMATCH. " +
                                $"DB expects {expectedContracts} contracts, broker shows {actualContracts}. " +
                                $"Possible partial fill. Check broker dashboard.");
                        }
                    }
                }
            }
        }

        if (anyChange)
            await uow.SaveChangesAsync();

        // ── Sync Position table from live broker data ─────────────────────────
        // Restricted to SignalId == null — these are the BROKER_SYNC rows this worker owns.
        // STRATEGY rows (SignalId set, created by BrokerOrderSyncWorker on fill) are never
        // matched/overwritten here, even when they share the same option Symbol.
        var existingPositions = (await positionRepo.FindAsync(p => p.SignalId == null))
            .ToDictionary(p => p.Symbol, StringComparer.OrdinalIgnoreCase);

        foreach (var bp in brokerPositions)
        {
            // AvgPrice = weighted avg from credit/debit legs; NetPrice = current LTP from broker
            var avgPrice     = (bp.DebitQuantity ?? 0) > 0 ? (bp.DebitPrice ?? 0m) : (bp.CreditPrice ?? 0m);
            var currentPrice = (bp.NetPrice ?? 0m) > 0 ? (bp.NetPrice ?? 0m) : avgPrice;
            var pnl          = (bp.RealisedPnl ?? 0m) + (currentPrice - avgPrice) * (bp.Quantity ?? 0);

            if (existingPositions.TryGetValue(bp.TradingSymbol, out var pos))
            {
                pos.Quantity     = bp.Quantity ?? 0;
                pos.AvgPrice     = avgPrice;
                pos.CurrentPrice = currentPrice;
                pos.PnL          = Math.Round(pnl, 2);
                pos.UpdatedOn    = IstClock.Now;
            }
            else if (bp.Quantity != 0)
            {
                await positionRepo.AddAsync(new Position
                {
                    Symbol         = bp.TradingSymbol,
                    Quantity       = bp.Quantity ?? 0,
                    AvgPrice       = avgPrice,
                    CurrentPrice   = currentPrice,
                    PnL            = Math.Round(pnl, 2),
                    PositionSource = "BROKER_SYNC",
                    UpdatedOn      = IstClock.Now,
                });
            }
        }

        // ── Cross-check STRATEGY positions against the broker mirror ─────────
        // Log-only: never auto-correct a signal-linked position from the broker snapshot.
        var strategyPositions = await positionRepo.FindAsync(
            p => p.PositionSource == "STRATEGY" && p.Status == "OPEN");

        foreach (var sp2 in strategyPositions)
        {
            var brokerMatch = brokerPositions.FirstOrDefault(p =>
                p.TradingSymbol.Equals(sp2.OptionSymbol, StringComparison.OrdinalIgnoreCase));
            var brokerQty = Math.Abs(brokerMatch?.Quantity ?? 0);

            if (brokerMatch is null || brokerQty != sp2.Quantity)
            {
                _logger.LogWarning(
                    "BrokerSync: STRATEGY position {Id} {OptionSymbol} qty={Qty} does not match broker ({BrokerQty}) — not auto-corrected",
                    sp2.Id, sp2.OptionSymbol, sp2.Quantity, brokerQty);
                await _errorLogger.LogWarningAsync(
                    "BrokerPositionSyncWorker",
                    new InvalidOperationException(
                        $"STRATEGY position {sp2.Id} ({sp2.OptionSymbol}) qty={sp2.Quantity} vs broker qty={brokerQty}"),
                    sp2.OptionSymbol,
                    $"SignalId={sp2.SignalId}; EntryOrderId={sp2.EntryOrderId}");
            }
        }

        // Zero-out positions no longer on broker
        var brokerSymbolSet = brokerPositions
            .Where(p => p.Quantity != 0)
            .Select(p => p.TradingSymbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var pos in existingPositions.Values.Where(p => !brokerSymbolSet.Contains(p.Symbol)))
        {
            pos.Quantity     = 0;
            pos.CurrentPrice = 0;
            pos.PnL          = 0;
            pos.UpdatedOn    = IstClock.Now;
        }

        await uow.SaveChangesAsync();

        // ── Orphan position detection ─────────────────────────────────────────
        // Broker has a position but no matching DB signal — could be a manual trade
        var dbOptionSymbols = dbSignals
            .Where(s => !string.IsNullOrEmpty(s.OptionSymbol))
            .Select(s => s.OptionSymbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var pos in brokerPositions.Where(p => p.Quantity != 0))
        {
            if (!dbOptionSymbols.Contains(pos.TradingSymbol))
            {
                _logger.LogWarning(
                    "BrokerSync: orphan position detected — {Symbol} qty={Qty} has no matching DB signal",
                    pos.TradingSymbol, pos.Quantity);
                await _alerts.SendCriticalAsync(
                    $"BrokerSync: Orphan position {pos.TradingSymbol} (qty={pos.Quantity}) found on broker with no matching signal in DB.");
            }
        }
    }
}

