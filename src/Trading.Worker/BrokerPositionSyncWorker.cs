using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;

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
    private readonly StopLossOrderCoordinator _stopLossCoordinator;

    // Only reconcile signals created in this window — avoids scanning full history
    private static readonly TimeSpan ReconcileWindow = TimeSpan.FromHours(8);

    /// <summary>
    /// How long a PENDING signal is left alone before its absence from the broker's position list
    /// is treated as evidence the order never reached the exchange. Must comfortably exceed a slow
    /// PlaceOrderAsync round-trip plus the lag before a fill appears in GetPositionsAsync.
    /// </summary>
    private static readonly TimeSpan PendingRejectGrace = TimeSpan.FromMinutes(5);

    public BrokerPositionSyncWorker(
        ILogger<BrokerPositionSyncWorker> logger,
        IServiceScopeFactory scopeFactory,
        ITradingAlertService alerts,
        AppErrorLogger errorLogger,
        StopLossOrderCoordinator stopLossCoordinator)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _alerts = alerts;
        _errorLogger = errorLogger;
        _stopLossCoordinator = stopLossCoordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
        var orderRepo    = sp.GetRequiredService<IRepository<Order>>();
        var portfolio    = sp.GetRequiredService<IGrowwPortfolioService>();
        var orders       = sp.GetRequiredService<IOrderService>();
        var uow          = sp.GetRequiredService<IUnitOfWork>();
        var instruments  = sp.GetRequiredService<IInstrumentCache>();

        // Load open DB signals from last 8 hours
        var windowStart = IstClock.Now - ReconcileWindow;

        // ── Broker-managed SL_M gap recovery ──────────────────────────────────
        // Covers a crash/restart (or a retry cap that was hit) between a position going ACTIVE
        // and its SL_M actually being placed — an open STRATEGY position must never sit
        // unprotected. Runs every cycle (cheap: no-ops once StopLossOrderId is set), not just
        // at startup, so it also catches a signal ExitWorker's self-heal hasn't gotten to yet.
        await RecoverMissingStopLossOrdersAsync(signalRepo, orderRepo, orders, uow, windowStart);
        var dbSignals = await signalRepo.FindAsync(
 s => s.CreatedOn >= windowStart
 &&
 (
     s.Status == SignalStatus.Pending ||
     s.Status == SignalStatus.OrderPlaced ||
     s.Status == SignalStatus.ExitPending ||
     s.Status == SignalStatus.PartialExitPending
 ));
        // Paper signals (incl. Scalping-state ones in Live mode) never exist at the broker: reconciling
        // them would reject pending ones and raise "no broker position" alerts.
        dbSignals = dbSignals.Where(s => !orders.IsPaperSignal(s)).ToList();
        if (dbSignals.Count == 0) return;

        // Fetch live broker positions
        List<Application.Models.PositionEntry> brokerPositions;
        try
        {
            brokerPositions = await portfolio.GetPositionsAsync();
        }
        catch (Exception)
        {
            return;
        }

        var brokerSymbols = brokerPositions
            .Select(p => p.TradingSymbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool anyChange = false;

        // Entry orders for every candidate signal, in one query — used only to decide whether
        // BrokerOrderSyncWorker can still resolve an order's fate.
        var candidateIds = dbSignals.Select(x => x.Id).ToList();
        var entryOrdersBySignal = (await orderRepo.FindAsync(
                o => o.SignalId.HasValue
                     && candidateIds.Contains(o.SignalId.Value)
                     && o.OrderPurpose == Trading.Domain.OrderPurpose.Entry))
            .GroupBy(o => o.SignalId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Order>)g.ToList());

        foreach (var signal in dbSignals)
        {
            var hasBrokerPosition = !string.IsNullOrEmpty(signal.OptionSymbol)
                && brokerSymbols.Contains(signal.OptionSymbol);

            if (signal.Status == SignalStatus.Pending)
            {
                // PENDING + no broker position = order never sent (crash between save and PlaceOrderAsync)
                //
                // GUARDED. GetPositionsAsync lags a fill by seconds, and a slow PlaceOrderAsync
                // round-trip can leave a signal PENDING for far longer than this worker's 30s
                // cycle. Declaring rejection on that evidence alone marked a LIVE filled position
                // as ORDER_REJECTED, which removed it from ExitDataLoader's open-signal query
                // (OrderPlaced/Active/PartialExitPending only) — so no stop was ever placed or
                // retried and the position had to be closed by hand.
                //
                // BrokerOrderSyncWorker is the source of truth for order fate; this worker must
                // not pre-empt it while reconciliation is still possible.
                entryOrdersBySignal.TryGetValue(signal.Id, out var entryOrders);

                var canReject = CanDeclarePendingRejected(
                    signal.CreatedOn, signal.BrokerOrderId, entryOrders, IstClock.Now,
                    PendingRejectGrace, out var rejectReason);

                if (!hasBrokerPosition && canReject)
                {
                    _logger.LogWarning(
                        "BrokerPositionSync: signal {Id} {Symbol} declared ORDER_REJECTED — {Reason}",
                        signal.Id, signal.Symbol, rejectReason);

                    signal.Status = SignalStatus.OrderRejected;
                    anyChange = true;
                }
                else if (!hasBrokerPosition)
                {
                    _logger.LogInformation(
                        "BrokerPositionSync: signal {Id} {Symbol} still PENDING with no broker position — not rejecting ({Reason})",
                        signal.Id, signal.Symbol, rejectReason);
                }
            }
            else if (signal.Status == SignalStatus.OrderPlaced)
            {
                if (!hasBrokerPosition)
                {
                    // ORDER_PLACED + no broker position = position closed outside app
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
        // Keyed by OptionSymbol (the broker's own trading_symbol) — the true 1:1 broker
        // identifier. Rows with no OptionSymbol yet (pre-fix legacy data) are excluded from
        // the lookup rather than risking a duplicate-key crash; they just get re-created
        // correctly below instead of being matched/updated in place.
        var existingPositions = (await positionRepo.FindAsync(p => p.SignalId == null))
            .Where(p => !string.IsNullOrEmpty(p.OptionSymbol))
            .ToDictionary(p => p.OptionSymbol, StringComparer.OrdinalIgnoreCase);

        // Snapshot once — GetActive() is a cheap field read, but resolving the underlying
        // per broker position would otherwise re-run the same Select/Where/OrderBy over it
        // for every row.
        var activeInstruments = instruments.GetActive();

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
                    Symbol         = ResolveUnderlying(bp.TradingSymbol, activeInstruments),
                    OptionSymbol   = bp.TradingSymbol,
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

        foreach (var pos in existingPositions.Values.Where(p => !brokerSymbolSet.Contains(p.OptionSymbol)))
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
                await _alerts.SendCriticalAsync(
                    $"BrokerSync: Orphan position {pos.TradingSymbol} (qty={pos.Quantity}) found on broker with no matching signal in DB.");
            }
        }
    }

    /// <summary>
    /// Maps a broker option contract symbol (e.g. "BANKNIFTY26AUG57600PE") to its underlying
    /// ("BANKNIFTY") via longest-prefix match against the active instrument list — Groww's
    /// position payload has no separate underlying field, only the contract's trading_symbol.
    /// Falls back to the raw broker symbol if no active instrument matches, rather than
    /// throwing, so one bad/inactive symbol can't take down the whole reconcile cycle.
    /// </summary>
    private static string ResolveUnderlying(string brokerSymbol, IReadOnlyList<TradingInstrument> activeInstruments) =>
        activeInstruments
            .Select(i => i.Symbol)
            .Where(sym => brokerSymbol.StartsWith(sym, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(sym => sym.Length)
            .FirstOrDefault() ?? brokerSymbol;

    /// <summary>
    /// Recreates a missing broker-managed SL_M for any ACTIVE single-leg signal that doesn't
    /// have one — the gap this covers is a crash/restart between BUY-complete and SL_M
    /// placement (or a placement that exhausted ExitWorker's retry cap while the app was down,
    /// so nothing was left running to self-heal it). An open position with no resting broker
    /// stop is exactly the failure mode this whole redesign exists to prevent, so it must never
    /// be left for "the next trailing-SL move" to notice.
    /// </summary>
    private async Task RecoverMissingStopLossOrdersAsync(
        IRepository<Signal> signalRepo,
        IRepository<Order> orderRepo,
        IOrderService orders,
        IUnitOfWork uow,
        DateTime windowStart)
    {
        if (orders.CurrentMode == TradingMode.Paper) return;

        var unprotected = await signalRepo.FindAsync(
            s => s.CreatedOn >= windowStart
              && s.Status == SignalStatus.Active
              && s.OptionSL > 0
              && s.StopLossOrderId == "");

        foreach (var signal in unprotected.Where(s => TradingConst.IsSingleLegStrategy(s.StrategyType)
                                                       && !orders.IsPaperSignal(s)))
        {
            _logger.LogWarning(
                "Recovering missing SL_M for signal {Id} {Symbol} (StopLossStatus={Status}, RetryCount={Retries}) — likely a restart/crash gap",
                signal.Id, signal.Symbol, signal.StopLossStatus, signal.StopLossRetryCount);

            var qty = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;
            var placed = await _stopLossCoordinator.TryPlaceAsync(signal, signal.OptionSL, qty, orders, orderRepo, signalRepo, uow);
            if (!placed)
            {
                await _alerts.SendCriticalAsync(
                    $"BrokerPositionSync: could not recover SL_M for signal #{signal.Id} ({signal.Symbol}) — position remains unprotected at broker level (StopLossRetryCount={signal.StopLossRetryCount}).");
            }
        }
    }

    /// <summary>
    /// True only when a PENDING signal's entry order can safely be declared never-placed.
    ///
    /// Refuses while any of these hold, because each means the order may be live at the broker:
    ///   * the signal is younger than the grace period (placement may still be in flight);
    ///   * the signal already carries a BrokerOrderId (the broker accepted it);
    ///   * an entry Order row carries a BrokerOrderId, or sits in a non-terminal state that
    ///     BrokerOrderSyncWorker is still reconciling (PENDING / ORDER_UNKNOWN).
    /// </summary>
    internal static bool CanDeclarePendingRejected(
        DateTime createdOn,
        string? signalBrokerOrderId,
        IReadOnlyList<Order>? entryOrders,
        DateTime now,
        TimeSpan grace,
        out string reason)
    {
        if (now - createdOn < grace)
        {
            reason = $"only {(now - createdOn).TotalSeconds:F0}s old, inside the {grace.TotalMinutes:F0}m placement grace";
            return false;
        }

        if (!string.IsNullOrEmpty(signalBrokerOrderId))
        {
            reason = $"signal already carries BrokerOrderId {signalBrokerOrderId}";
            return false;
        }

        if (entryOrders != null)
        {
            foreach (var o in entryOrders)
            {
                if (!string.IsNullOrEmpty(o.BrokerOrderId))
                {
                    reason = $"entry order {o.Id} carries BrokerOrderId {o.BrokerOrderId}";
                    return false;
                }

                if (o.Status == OrderStatus.Pending || o.Status == OrderStatus.OrderUnknown)
                {
                    reason = $"entry order {o.Id} is {o.Status} — BrokerOrderSyncWorker still reconciling";
                    return false;
                }
            }
        }

        reason = "past grace, no broker order id, no reconcilable entry order";
        return true;
    }
}
