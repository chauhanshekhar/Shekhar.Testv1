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
        // A non-positive trigger means the strategy deliberately has no option stop (HeroZero
        // sets OptionSL = 0). Placing an SL_M at trigger 0 would be an immediately-triggering sell
        // order, so refuse before touching the broker or the retry counter.
        if (triggerPrice <= 0)
            return false;

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

        // Deliberately NOT resetting StopLossRetryCount here. Broker ACCEPTANCE is not success:
        // Groww returns an order id, then rejects the order asynchronously seconds later (e.g.
        // "RMS:Margin Exceeds"), which lands in BrokerOrderSyncWorker and increments the count.
        // Resetting on acceptance made the pair cancel out — the count oscillated 0 -> 1 -> 0 and
        // the MaxStopLossRetries cap above could never be reached. Observed live on 2026-08-27,
        // signal 34: 21 rejected SL_M orders in 7 minutes with StopLossRetryCount still reading 1,
        // stopping only when the user cancelled the entry by hand.
        //
        // The budget is renewed in CancelAsync instead, on a CONFIRMED cancel — an order can only
        // be cancelled if it was genuinely resting at the exchange, which is the real proof of a
        // working stop that acceptance alone does not give.

        await uow.SaveChangesAsync();
        return true;
    }

    private static async Task MarkFailedAsync(Signal signal, Order slOrder, string? message, IUnitOfWork uow)
    {
        slOrder.Status = OrderStatus.Rejected;
        if (message != null) slOrder.StatusMessage = message;
        slOrder.UpdatedAt = IstClock.Now;
        signal.StopLossStatus = "FAILED";
        // Deliberately NOT stamping LastSlUpdateTime here. It means "when a stop was last actually
        // resting at the broker", which ExitWorker's MARKET fallback times from. Re-stamping it on
        // every FAILED attempt reset that timer roughly every second (SyncStopLossOrderAsync runs
        // immediately before DetermineExitReason in the same cycle), so the fallback's elapsed-time
        // check could never reach StopLossFallbackTimeoutSeconds and the position stayed
        // unprotected indefinitely while retries churned.
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
        => await CancelAsync(signal, orders, orderRepo, uow) == StopLossCancelResult.Cleared;

    /// <summary>
    /// Same cancel, but reports WHY it failed instead of collapsing every outcome into "false".
    /// That distinction is load-bearing for exits: a rejected/dead SL_M cannot fill and must never
    /// block a MARKET exit, whereas a genuinely resting or already-filled one must.
    /// </summary>
    public async Task<StopLossCancelResult> CancelAsync(
        Signal signal,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IUnitOfWork uow)
    {
        if (string.IsNullOrEmpty(signal.StopLossOrderId))
            return StopLossCancelResult.Cleared;

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
            catch (Exception) { /* leave status null — treated as Unconfirmed below */ }

            var reported = status?.Status ?? "";

            if (IsFilled(reported))
            {
                _logger.LogWarning(
                    "SL_M already COMPLETE for signal {Id} {Symbol} — broker sync will complete the exit",
                    signal.Id, signal.Symbol);
                return StopLossCancelResult.AlreadyFilled;
            }

            if (IsDead(reported))
            {
                // Rejected/cancelled at the broker: nothing is resting, so nothing can double-fill.
                // This is the common case when SL_M placement keeps failing (e.g. RMS margin
                // rejection), and treating it as a blocker is what previously held exits hostage.
                _logger.LogWarning(
                    "SL_M {OrderId} for signal {Id} {Symbol} is {Status} — no resting stop, exit may proceed",
                    stopLossOrderId, signal.Id, signal.Symbol, reported);
                return StopLossCancelResult.NotResting;
            }

            if (IsLive(reported))
            {
                _logger.LogError(
                    "SL_M {OrderId} for signal {Id} {Symbol} is still live at broker (status={Status}) "
                    + "and could not be cancelled — holding exit to avoid a double sell",
                    stopLossOrderId, signal.Id, signal.Symbol, reported);
                return StopLossCancelResult.StillResting;
            }

            _logger.LogError(
                "Could not confirm cancel or state of SL_M {OrderId} for signal {Id} {Symbol} (status={Status})",
                stopLossOrderId, signal.Id, signal.Symbol, string.IsNullOrEmpty(reported) ? "unknown" : reported);
            return StopLossCancelResult.Unconfirmed;
        }

        var oldRow = (await orderRepo.FindAsync(o => o.BrokerOrderId == stopLossOrderId)).FirstOrDefault();
        if (oldRow != null)
        {
            oldRow.Status = OrderStatus.Cancelled;
            oldRow.UpdatedAt = IstClock.Now;
        }

        signal.StopLossOrderId = "";
        signal.StopLossStatus = "";

        // Renew the retry budget here, not on placement. A cancel can only succeed against an
        // order that was actually resting at the exchange, so reaching this line is proof the
        // stop was genuinely working — unlike TryPlaceAsync, where the broker may accept an order
        // it rejects moments later. This keeps the normal trailing cancel-then-place cycle from
        // ever exhausting the cap, while a run of pure rejections now counts monotonically toward it.
        signal.StopLossRetryCount = 0;

        await uow.SaveChangesAsync();

        return StopLossCancelResult.Cleared;
    }

    // Groww reports NEW/ACKED/TRIGGER_PENDING/APPROVED/EXECUTED/COMPLETED/REJECTED/CANCELLED;
    // our own BrokerOrderStatus adds PENDING/OPEN/COMPLETE. Match both vocabularies.
    private static bool IsFilled(string s) =>
        s is "COMPLETE" or "COMPLETED" or "EXECUTED";

    private static bool IsDead(string s) =>
        s is "REJECTED" or "CANCELLED" or "FAILED";

    private static bool IsLive(string s) =>
        s is "OPEN" or "PENDING" or "NEW" or "ACKED" or "APPROVED" or "TRIGGER_PENDING";
}

/// <summary>Outcome of cancelling a resting SL_M, from the exit path's point of view.</summary>
public enum StopLossCancelResult
{
    /// <summary>Nothing was tracked, or the cancel was confirmed. Safe to sell.</summary>
    Cleared,

    /// <summary>The SL_M already filled — the position is closing on its own. Do NOT sell again.</summary>
    AlreadyFilled,

    /// <summary>Broker says the order is still live and it could not be cancelled. Do NOT sell.</summary>
    StillResting,

    /// <summary>Broker says the order is rejected/cancelled. Nothing can fill; safe to sell.</summary>
    NotResting,

    /// <summary>State could not be determined at all.</summary>
    Unconfirmed,
}
