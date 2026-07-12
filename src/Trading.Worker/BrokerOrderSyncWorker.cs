using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain;
using Trading.Domain.Entities;
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
    private static readonly TimeSpan Interval      = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReconcileWindow = TimeSpan.FromHours(8);
    private const int MaxSyncAttempts = 10;

    // Statuses that mean the order is done — no need to poll again
    private static readonly HashSet<string> TerminalStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "COMPLETE", "REJECTED", "CANCELLED", OrderStatus.ManualCheckRequired };

    public BrokerOrderSyncWorker(
        ILogger<BrokerOrderSyncWorker> logger,
        IServiceScopeFactory scopeFactory,
        ITradingAlertService alerts)
    {
        _logger       = logger;
        _scopeFactory = scopeFactory;
        _alerts       = alerts;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BrokerOrderSyncWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (MarketHours.IsOpen())
                    await SyncAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BrokerOrderSyncWorker error");
            }

            await Task.Delay(Interval, stoppingToken);
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

        _logger.LogDebug("BrokerOrderSync: checking {Count} unresolved orders", openOrders.Count);

        bool anyChange = false;

        foreach (var order in openOrders)
        {
            if (string.IsNullOrEmpty(order.BrokerOrderId))
            {
                anyChange |= await ResolveByReferenceAsync(order, orderService, signalRepo, positionRepo);
                continue;
            }

            BrokerOrderStatus? brokerStatus;
            try
            {
                brokerStatus = await orderService.GetOrderStatusAsync(order.BrokerOrderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BrokerOrderSync: could not fetch status for order {Id} ({BrokerId})",
                    order.Id, order.BrokerOrderId);
                continue;
            }

            if (brokerStatus is null)
            {
                // Order not found on broker — may have been cancelled on the dashboard
                _logger.LogWarning(
                    "BrokerOrderSync: order {Id} ({BrokerId}) not found on broker — possible external cancel",
                    order.Id, order.BrokerOrderId);
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

            _logger.LogInformation(
                "BrokerOrderSync: order {Id} {Symbol} {Side} → {Status} (filled={Filled}, avg={Avg:F2})",
                order.Id, order.Symbol, order.Side, normalized, brokerStatus.FilledQty, brokerStatus.AvgPrice);

            // Propagate terminal status to linked Signal
            await UpdateSignalStatusAsync(signalRepo, positionRepo, order, normalized, tradeCompletion);
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
        IRepository<Position> positionRepo)
    {
        var signal = order.SignalId.HasValue
            ? (await signalRepo.FindAsync(s => s.Id == order.SignalId.Value)).FirstOrDefault()
            : null;

        OrderDetail? detail;
        try
        {
            detail = await orderService.GetOrderByReferenceAsync(order.OrderReferenceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BrokerOrderSync: reference lookup failed for order {Id} (ref={Ref})",
                order.Id, order.OrderReferenceId);
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

        _logger.LogInformation(
            "BrokerOrderSync: resolved order {Id} via OrderReferenceId={Ref} → {Status}",
            order.Id, order.OrderReferenceId, normalized);

        if (signal != null)
        {
            signal.BrokerOrderId = detail.GrowwOrderId;
            signal.BrokerOrderStatus = normalized;

            if (normalized == "COMPLETE" && order.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase))
                await ActivateSignalAsync(signal, order, positionRepo);
            else if (normalized is "REJECTED" or "CANCELLED")
                signal.Status = SignalStatus.OrderRejected;
        }

        return true;
    }

    private async Task UpdateSignalStatusAsync(
        IRepository<Signal> signalRepo,
        IRepository<Position> positionRepo,
        Order order,
        string normalizedStatus,
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
                        positionRepo);
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
    private async Task ActivateSignalAsync(Signal signal, Order order, IRepository<Position> positionRepo)
    {
        signal.Status = SignalStatus.Active;

        var existing = await positionRepo.FindAsync(p => p.EntryOrderId == order.Id && p.PositionSource == "STRATEGY");
        if (existing.Count > 0) return;

        var filledQty = order.FilledQty > 0 ? order.FilledQty : order.Quantity;
        var avgPrice  = order.AvgPrice > 0 ? order.AvgPrice : order.Price;

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
    }
}
