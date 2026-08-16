using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.SignalEngine.Services;

/// <summary>
/// Places, replaces, and cancels the broker-managed SL_M order that protects an open
/// SINGLE/HERO_ZERO position. Single implementation shared by BrokerOrderSyncWorker (initial
/// placement on activation), ExitWorker (trailing replace + self-heal), and
/// BrokerPositionSyncWorker (restart/crash-gap recovery) — previously duplicated across the
/// first two, which is exactly the kind of drift that let inconsistencies creep in (e.g. one
/// call site tagging OrderPurpose and another forgetting to).
/// </summary>
public class StopLossOrderCoordinator
{
    // NSE/BSE F&O options both trade in 0.05 ticks — the exchange rejects any trigger/limit
    // price that isn't an exact multiple of this.
    private const decimal TickSize = 0.05m;

    private readonly ILogger<StopLossOrderCoordinator> _logger;
    private readonly SystemSettingsService _settingsService;

    public StopLossOrderCoordinator(ILogger<StopLossOrderCoordinator> logger, SystemSettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Places a fresh SL_M order (row-before-broker-call). Returns false without touching the
    /// broker if the signal has already exhausted its retry budget (MaxStopLossRetries) — the
    /// bounded MARKET-exit fallback in ExitWorker.DetermineExitReason is what protects the
    /// position at that point, not further placement attempts.
    /// </summary>
    public async Task<bool> TryPlaceAsync(
        Signal signal,
        decimal triggerPrice,
        int lots,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IRepository<Signal> signalRepo,
        IUnitOfWork uow)
    {
        var cfg = await _settingsService.GetAsync();
        if (signal.StopLossRetryCount >= cfg.MaxStopLossRetries)
        {
            _logger.LogWarning(
                "SL_M retry cap reached for signal {Id} {Symbol} ({Count}/{Max}) — not attempting placement; MARKET fallback protects the position if price crosses OptionSL",
                signal.Id, signal.Symbol, signal.StopLossRetryCount, cfg.MaxStopLossRetries);
            return false;
        }

        // Atomic claim, direct to the database — closes the race where BrokerOrderSyncWorker
        // (initial placement), ExitWorker (trailing replace/self-heal), and
        // BrokerPositionSyncWorker (crash recovery) can each independently observe
        // StopLossOrderId as empty in the same window and all attempt to place a resting SL_M
        // order for the same signal. A tracked-entity check-then-save can't prevent this since
        // each worker runs its own DbContext scope; ExecuteUpdateAsync bypasses the change
        // tracker and hits the row directly, so only the first caller's UPDATE can match the
        // WHERE clause — every other concurrent caller sees 0 rows affected and backs off
        // instead of placing a duplicate broker order.
        var claimed = await signalRepo.ExecuteUpdateAsync(
            s => s.Id == signal.Id && s.StopLossOrderId == "" && s.StopLossStatus != "PLACING",
            s => s.SetProperty(x => x.StopLossStatus, "PLACING"));

        if (claimed == 0)
        {
            _logger.LogInformation(
                "SL_M placement for signal {Id} {Symbol} skipped — already claimed by a concurrent caller this cycle",
                signal.Id, signal.Symbol);
            return false;
        }

        signal.StopLossStatus = "PLACING";

        // Round UP to the nearest tick — this is always a SELL protecting a long option, so
        // rounding toward entry only ever tightens the stop, never loosens it. An un-rounded
        // price (e.g. 348.79) is rejected outright by the exchange ("TRIGGER RATE NOT MULTIPLE
        // OF TICK"), which previously left the position placed with no resting stop at all.
        triggerPrice = Math.Ceiling(triggerPrice / TickSize) * TickSize;

        var slOrder = new Order
        {
            SignalId         = signal.Id,
            OrderReferenceId = Guid.NewGuid().ToString("N")[..20],
            Broker           = "GROWW",
            Symbol           = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange         = signal.Exchange,
            Side             = "SELL",
            OrderType        = GrowwOrderType.SlM,
            OrderPurpose     = Trading.Domain.OrderPurpose.StopLoss,
            ProductType      = "MIS",
            Status           = OrderStatus.Pending,
            TriggerPrice     = triggerPrice,
            Quantity         = lots,
            OrderedAt        = IstClock.Now,
            UpdatedAt        = IstClock.Now,
        };
        await orderRepo.StageAsync(slOrder);
        await uow.SaveChangesAsync();

        string brokerOrderId;
        try
        {
            brokerOrderId = await orders.PlaceStopLossOrderAsync(signal, triggerPrice, lots, slOrder.OrderReferenceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SL_M placement FAILED for signal {Id} {Symbol}", signal.Id, signal.Symbol);
            await MarkFailedAsync(signal, slOrder, ex.Message, uow);
            return false;
        }

        if (brokerOrderId == "SL_FAILED")
        {
            _logger.LogError("Broker rejected SL_M placement for signal {Id} {Symbol}", signal.Id, signal.Symbol);
            await MarkFailedAsync(signal, slOrder, null, uow);
            return false;
        }

        slOrder.BrokerOrderId = brokerOrderId;
        slOrder.UpdatedAt = IstClock.Now;

        signal.StopLossOrderId = brokerOrderId;
        signal.StopLossTriggerPrice = triggerPrice;
        signal.StopLossStatus = "PLACED";
        signal.LastSlUpdateTime = IstClock.Now;
        signal.StopLossRetryCount = 0;

        await uow.SaveChangesAsync();
        return true;
    }

    private static async Task MarkFailedAsync(Signal signal, Order slOrder, string? message, IUnitOfWork uow)
    {
        slOrder.Status = OrderStatus.Rejected;
        if (message != null) slOrder.StatusMessage = message;
        slOrder.UpdatedAt = IstClock.Now;
        signal.StopLossStatus = "FAILED";
        signal.LastSlUpdateTime = IstClock.Now;
        signal.StopLossRetryCount++;
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Cancels the resting SL_M order, if any. Returns true when it's safe for the caller to
    /// proceed (nothing was resting, or the cancel was confirmed) — false when the caller MUST
    /// NOT place a new SELL order this cycle (the SL_M may have already filled on the broker,
    /// or its fate is unconfirmed), to avoid a double sell. Callers should simply retry next
    /// cycle rather than force the issue.
    /// </summary>
    public async Task<bool> TryCancelAsync(
        Signal signal,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IUnitOfWork uow)
    {
        if (string.IsNullOrEmpty(signal.StopLossOrderId))
            return true;

        var stopLossOrderId = signal.StopLossOrderId;
        bool cancelled;
        try { cancelled = await orders.CancelOrderAsync(stopLossOrderId); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel SL_M FAILED for signal {Id} {Symbol}", signal.Id, signal.Symbol);
            cancelled = false;
        }

        if (!cancelled)
        {
            BrokerOrderStatus? status = null;
            try { status = await orders.GetOrderStatusAsync(stopLossOrderId); }
            catch (Exception) { /* leave status null — treated as unresolved below */ }

            if (status?.Status == "COMPLETE")
            {
                _logger.LogWarning(
                    "SL_M already COMPLETE for signal {Id} {Symbol} — broker sync will complete the exit",
                    signal.Id, signal.Symbol);
            }
            else
            {
                _logger.LogError(
                    "Could not confirm cancel of SL_M for signal {Id} {Symbol} (status={Status}) — will retry",
                    signal.Id, signal.Symbol, status?.Status ?? "unknown");
            }

            return false;
        }

        var oldRow = (await orderRepo.FindAsync(o => o.BrokerOrderId == stopLossOrderId)).FirstOrDefault();
        if (oldRow != null)
        {
            oldRow.Status = OrderStatus.Cancelled;
            oldRow.UpdatedAt = IstClock.Now;
        }

        signal.StopLossOrderId = "";
        signal.StopLossStatus = "";
        await uow.SaveChangesAsync();

        return true;
    }
}
