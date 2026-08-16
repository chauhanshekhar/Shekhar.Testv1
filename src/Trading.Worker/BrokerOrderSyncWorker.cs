using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.Worker;

/// <summary>
/// Every 30 seconds reconciles the Order table against the broker's order book.
///
/// Two lookup paths per unresolved order:
///   - BrokerOrderId known   → GetOrderStatusAsync (existing polling path)
///   - BrokerOrderId unknown → GetOrderByReferenceAsync(OrderReferenceId) — recovers an
///     order whose placement response never arrived (timeout/network failure). Every
///     attempt on this path increments Signal.OrderSyncAttempts; after 10 with no
///     resolution the order/signal move to MANUAL_CHECK_REQUIRED and stop being polled.
///
/// Terminal statuses that end reconciliation: COMPLETE / REJECTED / CANCELLED / MANUAL_CHECK_REQUIRED
///
/// On COMPLETE for an entry (BUY) order: Signal.Status = ACTIVE and a STRATEGY Position row
/// is created (guarded by EntryOrderId so re-runs don't duplicate it) — never touches the
/// BROKER_SYNC rows BrokerPositionSyncWorker owns.
/// </summary>
public class BrokerOrderSyncWorker : BackgroundService
{
    private readonly ILogger<BrokerOrderSyncWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradingAlertService _alerts;
    private readonly StopLossOrderCoordinator _stopLossCoordinator;

    // 5s during market hours — a resting SL_M fill must be detected fast (this is the whole
    // point of moving the stop loss to the broker); 30s outside hours since nothing is moving.
    private static readonly TimeSpan MarketOpenInterval   = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MarketClosedInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReconcileWindow = TimeSpan.FromHours(8);
    private const int MaxSyncAttempts = 10;

    // Statuses that mean the order is done — no need to poll again
    private static readonly HashSet<string> TerminalStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "COMPLETE", "REJECTED", "CANCELLED", OrderStatus.ManualCheckRequired };

    public BrokerOrderSyncWorker(
        ILogger<BrokerOrderSyncWorker> logger,
        IServiceScopeFactory scopeFactory,
        ITradingAlertService alerts,
        StopLossOrderCoordinator stopLossCoordinator)
    {
        _logger       = logger;
        _scopeFactory = scopeFactory;
        _alerts       = alerts;
        _stopLossCoordinator = stopLossCoordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var marketOpen = MarketHours.IsOpen();
            try
            {
                if (marketOpen)
                    await SyncAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BrokerOrderSyncWorker error");
            }

            await Task.Delay(marketOpen ? MarketOpenInterval : MarketClosedInterval, stoppingToken);
        }
    }

    private async Task SyncAsync()
    {
        using var scope      = _scopeFactory.CreateScope();
        var sp               = scope.ServiceProvider;
        var orderRepo        = sp.GetRequiredService<IRepository<Order>>();
        var signalRepo       = sp.GetRequiredService<IRepository<Signal>>();
        var positionRepo     = sp.GetRequiredService<IRepository<Position>>();
        var tradeCompletion =sp.GetRequiredService<TradeCompletionService>();
        var orderService     = sp.GetRequiredService<IOrderService>();
        var uow              = sp.GetRequiredService<IUnitOfWork>();

        var windowStart = IstClock.Now - ReconcileWindow;

        // Load orders that are NOT yet in a terminal state
        var openOrders = await orderRepo.FindAsync(
            o => o.OrderedAt >= windowStart && !TerminalStatuses.Contains(o.Status));

        if (openOrders.Count == 0) return;

        bool anyChange = false;

        foreach (var order in openOrders)
        {
            if (string.IsNullOrEmpty(order.BrokerOrderId))
            {
                anyChange |= await ResolveByReferenceAsync(order, orderService, signalRepo, positionRepo, orderRepo, uow, tradeCompletion);
                continue;
            }

            BrokerOrderStatus? brokerStatus;
            try
            {
                brokerStatus = await orderService.GetOrderStatusAsync(order.BrokerOrderId);
            }
            catch (Exception)
            {
                continue;
            }

            if (brokerStatus is null)
            {
                // Order not found on broker — may have been cancelled on the dashboard
                await _alerts.SendCriticalAsync(
                    $"BrokerOrderSync: order #{order.Id} ({order.Symbol} {order.Side}) " +
                    $"not found on broker. May have been cancelled externally. BrokerOrderId={order.BrokerOrderId}");
                continue;
            }

            var normalized = brokerStatus.Status.ToUpperInvariant();

            if (normalized == order.Status.ToUpperInvariant()) continue; // no change

            // Update order fields
            order.Status        = normalized;
            order.StatusMessage = brokerStatus.StatusMessage;
            order.FilledQty     = brokerStatus.FilledQty;
            order.AvgPrice      = brokerStatus.AvgPrice;
            order.UpdatedAt     = IstClock.Now;
            anyChange = true;

            // Propagate terminal status to linked Signal
            await UpdateSignalStatusAsync(signalRepo, positionRepo, order, normalized, orderRepo, orderService, uow, tradeCompletion);
        }

        if (anyChange)
            await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Recovers an order whose BrokerOrderId was never confirmed (timeout/network failure
    /// during placement) via OUR OrderReferenceId, which was sent to Groww as-is. Every call
    /// counts as one sync attempt against the linked Signal, whether or not it resolves.
    /// </summary>
    private async Task<bool> ResolveByReferenceAsync(
        Order order,
        IOrderService orderService,
        IRepository<Signal> signalRepo,
        IRepository<Position> positionRepo,
        IRepository<Order> orderRepo,
        IUnitOfWork uow,
        TradeCompletionService tradeCompletion)
    {
        var signal = order.SignalId.HasValue
            ? (await signalRepo.FindAsync(s => s.Id == order.SignalId.Value)).FirstOrDefault()
            : null;

        OrderDetail? detail;
        try
        {
            detail = await orderService.GetOrderByReferenceAsync(order.OrderReferenceId);
        }
        catch (Exception)
        {
            detail = null;
        }

        if (signal != null)
        {
            signal.OrderSyncAttempts++;
            signal.LastOrderSyncTime = IstClock.Now;
        }

        if (detail is null)
        {
            if (signal != null && signal.OrderSyncAttempts >= MaxSyncAttempts)
            {
                order.Status = OrderStatus.ManualCheckRequired;
                order.UpdatedAt = IstClock.Now;
                signal.Status = SignalStatus.ManualCheckRequired;

                _logger.LogError(
                    "BrokerOrderSync: order {Id} ({Symbol}) unresolved after {N} attempts — MANUAL_CHECK_REQUIRED",
                    order.Id, order.Symbol, signal.OrderSyncAttempts);
                await _alerts.SendCriticalAsync(
                    $"MANUAL CHECK REQUIRED — order #{order.Id} ({order.Symbol}) could not be reconciled " +
                    $"after {MaxSyncAttempts} attempts. OrderReferenceId={order.OrderReferenceId}");
            }
            return true; // attempt count/timestamp changed even though nothing resolved
        }

        var normalized = (detail.OrderStatus ?? "").ToUpperInvariant();
        order.BrokerOrderId = detail.GrowwOrderId;
        order.Status = string.IsNullOrEmpty(normalized) ? OrderStatus.Pending : normalized;
        order.FilledQty = detail.FilledQuantity ?? 0;
        order.AvgPrice = detail.AverageFillPrice ?? 0m;
        order.RawPostback = JsonSerializer.Serialize(detail);
        order.UpdatedAt = IstClock.Now;

        if (signal != null)
        {
            // Broker-managed SL_M rows are resting protective orders, not "the" entry/exit of
            // the signal — mirror UpdateSignalStatusAsync's guard so a fill/rejection/cancel
            // discovered via this reference-lookup path (BrokerOrderId was unresolved when this
            // order was placed) can't clobber signal.BrokerOrderId/BrokerOrderStatus or misfire
            // the generic ENTRY/EXIT handling below. Without this, an SL_M rejection here used
            // to flip an otherwise-healthy ACTIVE signal straight to ORDER_REJECTED, pulling a
            // live filled position out of ExitWorker's monitoring entirely.
            if (order.OrderPurpose == Trading.Domain.OrderPurpose.StopLoss)
            {
                switch (normalized)
                {
                    case "COMPLETE":
                        await tradeCompletion.CompleteExitAsync(signal, order, TradingConst.ExitReason.OptionSlHit);
                        signal.StopLossOrderId = "";
                        signal.StopLossStatus = "";
                        break;
                    case "REJECTED":
                        signal.StopLossStatus = "FAILED";
                        break;
                    case "CANCELLED":
                        signal.StopLossStatus = "CANCELLED";
                        break;
                }
                return true;
            }

            signal.BrokerOrderId = detail.GrowwOrderId;
            signal.BrokerOrderStatus = normalized;

            if (normalized == "COMPLETE")
            {
                if (order.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase))
                {
                    await ActivateSignalAsync(signal, order, positionRepo, orderRepo, signalRepo, orderService, uow);
                }
                else if (order.Side.Equals("SELL", StringComparison.OrdinalIgnoreCase))
                {
                    if (signal.Status == SignalStatus.ExitPending)
                        await tradeCompletion.CompleteExitAsync(signal, order, signal.ExitReason ?? "UNKNOWN");
                    else if (signal.Status == SignalStatus.PartialExitPending)
                        await tradeCompletion.CompletePartialExitAsync(signal, order);
                }
            }
            else if (normalized is "REJECTED" or "CANCELLED")
            {
                signal.Status = SignalStatus.OrderRejected;
            }
        }

        return true;
    }

    private async Task UpdateSignalStatusAsync(
        IRepository<Signal> signalRepo,
        IRepository<Position> positionRepo,
        Order order,
        string normalizedStatus,
        IRepository<Order> orderRepo,
        IOrderService orders,
        IUnitOfWork uow,
        TradeCompletionService tradeCompletion)
    {
        Signal? signal = null;

        if (order.SignalId.HasValue)
        {
            signal = (await signalRepo.FindAsync(
                s => s.Id == order.SignalId.Value))
                .FirstOrDefault();
        }

        if (signal == null &&
            !string.IsNullOrEmpty(order.BrokerOrderId))
        {
            signal = (await signalRepo.FindAsync(
                s =>
                s.BrokerOrderId != null &&
                s.BrokerOrderId.Contains(order.BrokerOrderId)))
                .FirstOrDefault();
        }

        if (signal == null)
            return;


        // Broker-managed SL_M rows are resting protective orders, not "the" entry/exit of the
        // signal — they must never drive signal.Status the way a normal BUY/SELL order does.
        // Handled separately so a rejected/cancelled SL_M placement can never misfire the
        // generic ENTRY/EXIT switch below and flip an otherwise-healthy ACTIVE signal to
        // ORDER_REJECTED. Identified by OrderPurpose, not OrderType — OrderType only says HOW
        // the order executes (SL_M/MARKET/LIMIT); OrderPurpose says WHY it exists, which is what
        // this branch actually needs (and stays correct if a future order type also uses SL_M).
        if (order.OrderPurpose == Trading.Domain.OrderPurpose.StopLoss)
        {
            switch (normalizedStatus)
            {
                case "COMPLETE":
                    await tradeCompletion.CompleteExitAsync(signal, order, TradingConst.ExitReason.OptionSlHit);
                    signal.StopLossOrderId = "";
                    signal.StopLossStatus = "";
                    break;
                case "REJECTED":
                    signal.StopLossStatus = "FAILED";
                    break;
                case "CANCELLED":
                    signal.StopLossStatus = "CANCELLED";
                    break;
            }
            return;
        }

        switch (normalizedStatus)
        {
            case "COMPLETE":

                signal.BrokerOrderStatus = normalizedStatus;


                // ENTRY
                if (order.Side.Equals(
                    "BUY",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await ActivateSignalAsync(
                        signal,
                        order,
                        positionRepo,
                        orderRepo,
                        signalRepo,
                        orders,
                        uow);
                }


                // EXIT
                else if (order.Side.Equals(
            "SELL",
            StringComparison.OrdinalIgnoreCase))
                {

                    if (signal.Status == SignalStatus.ExitPending)
                    {
                        await tradeCompletion.CompleteExitAsync(
                            signal,
                            order,
                            signal.ExitReason!);
                    }


                    else if (signal.Status == SignalStatus.PartialExitPending)
                    {
                        await tradeCompletion.CompletePartialExitAsync(
                            signal,
                            order);
                    }
                }

                break;


            case "REJECTED":

                signal.Status =
                    SignalStatus.OrderRejected;

                signal.BrokerOrderStatus =
                    normalizedStatus;

                break;


            case "CANCELLED":

                signal.Status =
                    SignalStatus.OrderRejected;

                signal.BrokerOrderStatus =
                    normalizedStatus;

                break;
        }
    }

    /// <summary>
    /// Signal.Status = ACTIVE + create the signal-linked STRATEGY Position row. Guarded by
    /// EntryOrderId so repeated reconciliation cycles (or both lookup paths noticing the
    /// same COMPLETE fill) never create a duplicate.
    /// </summary>
    private async Task ActivateSignalAsync(
        Signal signal,
        Order order,
        IRepository<Position> positionRepo,
        IRepository<Order> orderRepo,
        IRepository<Signal> signalRepo,
        IOrderService orders,
        IUnitOfWork uow)
    {
        // A BUY-fill confirmation arriving after the signal has already progressed past entry
        // (e.g. both legs were stuck PENDING and resolve in the same reconcile pass) must not
        // regress it back to ACTIVE — that would reopen an already-exited position for ExitWorker.
        if (signal.Status != SignalStatus.Pending && signal.Status != SignalStatus.OrderPlaced)
            return;

        signal.Status = SignalStatus.Active;

        var existing = await positionRepo.FindAsync(p => p.EntryOrderId == order.Id && p.PositionSource == "STRATEGY");
        if (existing.Count > 0) return;

        var filledQty = order.FilledQty > 0 ? order.FilledQty : order.Quantity;
        var avgPrice  = order.AvgPrice > 0 ? order.AvgPrice : order.Price;

        // Re-anchor OptionSL/OptionT1/OptionT2 to the actual fill price. SignalBuilder computes
        // these against OptionLtp quoted at signal-creation time, before this BUY order existed —
        // if the LIMIT order fills away from that quote (routine for options), the resting SL_M
        // trigger stays pinned to the stale quote, silently changing the real risk-to-stop from
        // what the strategy intended (and TradeCompletionService's P&L, which reads OptionLtp as
        // the entry price, would misreport against the quote instead of what was actually paid).
        // Preserve the same risk/reward RATIO computed at signal-creation — works uniformly for
        // both ATR-points-derived stops (Directional) and flat-percentage stops (HeroZero) — just
        // recentre it on what was actually paid.
        if (avgPrice > 0 && signal.OptionLtp > 0 && avgPrice != signal.OptionLtp)
        {
            var slRatio = signal.OptionSL / signal.OptionLtp;
            var t1Ratio = signal.OptionT1 / signal.OptionLtp;
            var t2Ratio = signal.OptionT2 / signal.OptionLtp;

            signal.OptionSL  = Math.Round(avgPrice * slRatio, 2);
            signal.OptionT1  = Math.Round(avgPrice * t1Ratio, 2);
            signal.OptionT2  = Math.Round(avgPrice * t2Ratio, 2);
            signal.OptionLtp = avgPrice;
        }

        await positionRepo.AddAsync(new Position
        {
            SignalId       = signal.Id,
            EntryOrderId   = order.Id,
            Symbol         = signal.Symbol,
            OptionSymbol   = order.Symbol,
            Quantity       = filledQty,
            AvgPrice       = avgPrice,
            CurrentPrice   = avgPrice,
            Status         = "OPEN",
            PositionSource = "STRATEGY",
            OpenedAt       = IstClock.Now,
            UpdatedOn      = IstClock.Now,
        });

        await PlaceInitialStopLossAsync(signal, orderRepo, signalRepo, orders, uow);
    }

    /// <summary>
    /// Places the broker-managed SL_M stop loss immediately after a SINGLE/HERO_ZERO position
    /// activates — Groww's matching engine, not ExitWorker's 1s poll, now owns the stop-loss
    /// trigger. Spreads (BULL_SPREAD/BEAR_SPREAD) are skipped — their OptionSL is a net two-leg
    /// premium and can't be a single broker order (see TradingConst.IsSingleLegStrategy).
    /// Delegates the actual placement to StopLossOrderCoordinator (shared with ExitWorker's
    /// trailing-replace/self-heal and BrokerPositionSyncWorker's restart recovery). On failure
    /// a critical alert is raised immediately — ExitWorker retries every cycle up to
    /// MaxStopLossRetries and has a bounded last-resort MARKET-exit fallback, but a live
    /// position with no resting broker stop for even a few seconds is exactly the failure mode
    /// this whole change exists to avoid, so it must never fail silently.
    /// </summary>
    private async Task PlaceInitialStopLossAsync(
        Signal signal,
        IRepository<Order> orderRepo,
        IRepository<Signal> signalRepo,
        IOrderService orders,
        IUnitOfWork uow)
    {
        if (orders.CurrentMode == TradingMode.Paper) return;
        if (!TradingConst.IsSingleLegStrategy(signal.StrategyType)) return;
        if (signal.OptionSL <= 0) return;

        var placed = await _stopLossCoordinator.TryPlaceAsync(signal, signal.OptionSL, signal.Quantity, orders, orderRepo, signalRepo, uow);
        if (!placed)
        {
            await _alerts.SendCriticalAsync(
                $"Broker SL_M placement FAILED for signal #{signal.Id} ({signal.Symbol}) — position is unprotected at broker level (StopLossRetryCount={signal.StopLossRetryCount}). ExitWorker will retry up to the configured cap.");
        }
    }
}
