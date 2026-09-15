using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.Worker.Exits;

/// <summary>
/// Broker-managed SL_M lifecycle as seen from the exit path. Extracted from ExitWorker unchanged;
/// all placement/cancellation still delegates to <see cref="StopLossOrderCoordinator"/>, which
/// stays the single owner of retry-cap and OrderPurpose bookkeeping across ExitWorker,
/// BrokerOrderSyncWorker (initial placement) and BrokerPositionSyncWorker (restart recovery).
/// </summary>
public class StopLossSyncService
{
    private readonly StopLossOrderCoordinator _stopLossCoordinator;

    public StopLossSyncService(StopLossOrderCoordinator stopLossCoordinator)
    {
        _stopLossCoordinator = stopLossCoordinator;
    }

    // ── Sync (self-heal / trailing replace) ─────────────────────────────────────────────────
    // Called every cycle for active SINGLE/HERO_ZERO live signals, after the trailing stop may
    // have moved signal.OptionSL. Cancel-then-place, never place-then-cancel: two simultaneous
    // resting SELL orders risk a double fill, which is strictly worse than a brief protection
    // gap during the cancel→place window.
    public async Task SyncAsync(
        Signal signal,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IRepository<Signal> signalRepo,
        IUnitOfWork uow)
    {
        if (signal.OptionSL <= 0) return;

        // Past the broker's intraday cutoff every SL_M placement is rejected on arrival, so
        // attempting one only produces rejection churn — and, worse, briefly flips
        // StopLossStatus back to PLACED, which suppresses the polling MARKET fallback in
        // ExitDecisionService (gated on StopLossStatus != "PLACED") that is the ONLY protection
        // still available in this window. Leaving the status alone here is what lets that
        // fallback engage.
        if (MarketHours.IsPastBrokerIntradayCutoff()) return;

        var desiredQty = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;

        // Self-heal trigger is "we have no protection", NOT merely "no id recorded". A broker
        // rejection can leave a dead StopLossOrderId behind while StopLossStatus reads FAILED;
        // keying only off the id made this method fall through to the trailing-replace path,
        // which then returned early on the tolerance check (trigger price still equals OptionSL)
        // and never re-placed anything. Defensive belt-and-braces alongside BrokerOrderSyncWorker
        // now clearing the id at the source.
        var hasNoRestingStop =
            string.IsNullOrEmpty(signal.StopLossOrderId) ||
            signal.StopLossStatus == "FAILED" ||
            signal.StopLossStatus == "CANCELLED";

        if (hasNoRestingStop)
        {
            signal.StopLossOrderId = "";

            // MUST be persisted before TryPlaceAsync. That method claims the row with
            // ExecuteUpdateAsync(... WHERE StopLossOrderId == ""), which is raw SQL: it bypasses
            // the change tracker and reads what is COMMITTED, not what is pending in memory.
            // Without this save the dead id is still in the database, the claim matches 0 rows,
            // the coordinator backs off as "already claimed", and the position stays with no
            // resting stop — precisely the failure this self-heal exists to fix.
            await uow.SaveChangesAsync();

            // Self-heals a placement that failed at activation, AND handles the case right after
            // a partial exit completes elsewhere (TradeCompletionService.CompletePartialExitAsync
            // resets OptionSL to breakeven but doesn't itself talk to the broker). Internally a
            // no-op once StopLossRetryCount hits the configured cap.
            await _stopLossCoordinator.TryPlaceAsync(signal, signal.OptionSL, desiredQty, orders, orderRepo, signalRepo, uow);
            return;
        }

        // Tolerance guards float/rounding noise so a no-op cycle doesn't churn cancel/replace.
        if (Math.Abs(signal.StopLossTriggerPrice - signal.OptionSL) < 0.05m)
            return;

        if (!await _stopLossCoordinator.TryCancelAsync(signal, orders, orderRepo, uow))
            return; // already filled, or unconfirmed — coordinator logged the reason; retry next cycle

        await _stopLossCoordinator.TryPlaceAsync(signal, signal.OptionSL, desiredQty, orders, orderRepo, signalRepo, uow);
    }

    // ── Cancel before any MARKET sell ───────────────────────────────────────────────────────
    // Called from the close/partial-exit paths before T1/T2/ForceExit/HeroZero/ProtectedSL/
    // IndexSL fire a MARKET sell, so the resting SL_M can't also fill (double sell). MUST run
    // before the partial/full SELL is placed, never after — cancelling only after the market
    // sell would leave a window where both orders are live simultaneously. The caller must abort
    // the MARKET exit only for AlreadyFilled / StillResting — a REJECTED or CANCELLED stop
    // (NotResting) cannot fill and must never hold an exit up, which is what previously stranded
    // profitable target exits while SL_M placement was failing on RMS margin.
    public Task<StopLossCancelResult> CancelBeforeMarketExitAsync(
        Signal signal,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IUnitOfWork uow)
        => _stopLossCoordinator.CancelAsync(signal, orders, orderRepo, uow);
}
