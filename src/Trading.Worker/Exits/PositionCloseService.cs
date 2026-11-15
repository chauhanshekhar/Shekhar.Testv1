using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.Worker.Exits;

/// <summary>
/// Places the SELL that actually closes a position — full exit or T1 partial. Extracted from
/// ExitWorker unchanged.
///
/// Both paths follow the same crash-safety contract as the entry flow: the Order row is inserted
/// and saved BEFORE the broker is called, so a crash or timeout mid-close leaves a visible
/// PENDING row rather than a lost order, and BrokerOrderSyncWorker reconciles it. Neither path
/// writes a TradeJournal — BrokerOrderSyncWorker owns completion once the broker reports
/// COMPLETE.
/// </summary>
public class PositionCloseService
{
    private readonly ILogger<PositionCloseService> _logger;
    private readonly IInstrumentCache _instruments;
    private readonly StopLossSyncService _stopLossSync;

    public PositionCloseService(
        ILogger<PositionCloseService> logger,
        IInstrumentCache instruments,
        StopLossSyncService stopLossSync)
    {
        _logger = logger;
        _instruments = instruments;
        _stopLossSync = stopLossSync;
    }

    // ── Place close order + update signal status ──────────────────────────────
    // Returns the exit Order's Id on success, or null.
    public async Task<long?> ClosePositionAsync(
        Signal signal,
        string exitReason,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IUnitOfWork uow,
        bool forceExit = false)
    {
        var finalLots = signal.RemainingQty > 0
            ? signal.RemainingQty
            : signal.PartialPnL == 0 ? signal.Quantity : 0;

        if (finalLots <= 0)
        {
            return null;
        }

        if (!await HasConfirmedEntryFillAsync(signal, orderRepo))
        {
            _logger.LogError(
                "Refusing {Reason} SELL for signal {Id} {Symbol} — entry never filled "
                + "(Status={Status}, BrokerOrderStatus={BrokerStatus}). Selling here would OPEN a "
                + "short position rather than close a long one.",
                exitReason, signal.Id, signal.Symbol, signal.Status, signal.BrokerOrderStatus);
            return null;
        }

        // Abort ONLY when a stop could genuinely still fill. Previously any cancel failure aborted
        // the exit, which meant a stop that the broker kept REJECTING (e.g. RMS margin) blocked
        // every MARKET exit — including hit targets — for as long as the rejections continued.
        var cancelResult = await _stopLossSync.CancelBeforeMarketExitAsync(signal, orders, orderRepo, uow);
        switch (cancelResult)
        {
            case StopLossCancelResult.AlreadyFilled:
                // The stop is closing the position itself; selling again would open a short.
                return null;

            case StopLossCancelResult.StillResting when !forceExit:
                // Genuinely live at the broker and uncancellable — retry next cycle rather than
                // risk both orders filling. Force exit overrides: an unexited position past 15:30
                // is worse, since ExitWorker stops running and nothing can close it.
                return null;

            case StopLossCancelResult.StillResting:
                _logger.LogWarning(
                    "FORCE EXIT proceeding for signal {Id} {Symbol} with an uncancellable resting SL_M "
                    + "({SlId}) — accepting double-fill risk over an unexited position",
                    signal.Id, signal.Symbol, signal.StopLossOrderId);
                break;

            case StopLossCancelResult.Unconfirmed:
                _logger.LogWarning(
                    "Exit for signal {Id} {Symbol} proceeding with UNCONFIRMED SL_M state ({SlId}, reason={Reason}) "
                    + "— holding the exit has proven costlier than the double-fill risk",
                    signal.Id, signal.Symbol, signal.StopLossOrderId, exitReason);
                break;

            case StopLossCancelResult.NotResting:
            case StopLossCancelResult.Cleared:
            default:
                break;
        }

        var exitOrder = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = Guid.NewGuid().ToString("N")[..20],
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "SELL",
            OrderType = "MARKET",
            OrderPurpose = MapExitReasonToPurpose(exitReason),
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Quantity = finalLots,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await orderRepo.StageAsync(exitOrder);
        await uow.SaveChangesAsync();

        string closeOrderId;
        try { closeOrderId = await orders.ClosePositionAsync(signal, exitReason, finalLots, exitOrder.OrderReferenceId); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Close order FAILED for signal {Id} {Symbol}", signal.Id, signal.Symbol);
            signal.Status = SignalStatus.CloseFailed;
            exitOrder.Status = OrderStatus.Failed;
            exitOrder.StatusMessage = ex.Message;
            exitOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return null;
        }

        if (closeOrderId == "CLOSE_FAILED")
        {
            _logger.LogError("Broker rejected close for signal {Id} {Symbol}", signal.Id, signal.Symbol);
            signal.Status = SignalStatus.CloseFailed;
            exitOrder.Status = OrderStatus.Failed;
            exitOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return null;
        }

        // Groww accepted the close — fill status isn't known yet, stays PENDING for
        // BrokerOrderSyncWorker to confirm, same as the entry flow.
        exitOrder.BrokerOrderId = closeOrderId;
        exitOrder.Status = OrderStatus.Pending;
        exitOrder.UpdatedAt = IstClock.Now;

        signal.Status = SignalStatus.ExitPending;
        signal.ExitReason = exitReason;

        await uow.SaveChangesAsync();

        return exitOrder.Id;
    }

    /// <summary>
    /// True only when the entry is confirmed filled, so a SELL is actually backed by a long
    /// position.
    ///
    /// SignalStatus.OrderPlaced means "entry order sent", NOT "entry filled" — only
    /// BrokerOrderSyncWorker.ActivateSignalAsync promotes a signal to Active, and it does that
    /// solely on a BUY-fill confirmation. ExitDataLoader deliberately loads OrderPlaced signals
    /// too (a real fill whose confirmation is merely late must still be exitable), so the
    /// not-yet-filled case has to be excluded here, at the one place that actually sends a SELL.
    ///
    /// Without this, an entry that is REJECTED or CANCELLED with FilledQty=0 still gets target
    /// and exit orders placed against it — observed live on 2026-08-27, signals 23 and 31, where
    /// only the account being out of margin stopped those SELLs from filling and opening
    /// unintended SHORT positions.
    /// </summary>
    private static async Task<bool> HasConfirmedEntryFillAsync(Signal signal, IRepository<Order> orderRepo)
    {
        // Only query the entry orders when the status alone can't settle it.
        if (IsEntryFillConfirmed(signal.Status, entryFilledQty: 0))
            return true;

        // Still OrderPlaced/Pending: trust the entry order itself, so a late fill confirmation
        // doesn't strand a genuinely open position.
        var entries = await orderRepo.FindAsync(o =>
            o.SignalId == signal.Id && o.OrderPurpose == OrderPurpose.Entry);

        var filled = entries.Count == 0 ? 0 : entries.Max(o => o.FilledQty);
        return IsEntryFillConfirmed(signal.Status, filled);
    }

    /// <summary>The pure decision behind <see cref="HasConfirmedEntryFillAsync"/>. Exposed for testing.</summary>
    internal static bool IsEntryFillConfirmed(string status, int entryFilledQty)
        => status is SignalStatus.Active
                  or SignalStatus.PartialExitPending
                  or SignalStatus.ExitPending
                  or SignalStatus.CloseFailed
           || entryFilledQty > 0;

    // ── Partial exit (T1 hit, multiple lots) ──────────────────────────────────
    public async Task HandlePartialExitAsync(
        Signal signal,
        decimal optionLtp,
        DateTime nowIst,
        Dictionary<string, List<OptionChainData>> chainBySymbol,
        IOrderService orders,
        IRepository<TradeJournal> journalRepo,
        IRepository<Order> orderRepo,
        IUnitOfWork uow,
        TradeCompletionService tradeCompletion,
        int stage = 1,
        string exitReason = TradingConst.ExitReason.OptionT1Hit)
    {
        // One partial sale per STAGE (1 = T1 or breakeven-arm sale, 2 = target-ladder middle
        // target), not one per trade. Skipped when that many partial fills are already journalled,
        // or when a partial SELL is still in flight at the broker - never two SELLs at once.
        var completedPartials =
            (await journalRepo.FindAsync(j => j.SignalId == signal.Id && j.IsPartial)).Count;
        var inFlight =
            await orderRepo.FirstOrDefaultAsync(
                o => o.SignalId == signal.Id
                  && o.OrderPurpose == Trading.Domain.OrderPurpose.PartialExit
                  && o.Status == OrderStatus.Pending);
        if (!ShouldPlacePartial(stage, completedPartials, inFlight != null)) return;

        // Same rule as the full close — never sell against an entry that never filled.
        if (!await HasConfirmedEntryFillAsync(signal, orderRepo))
        {
            _logger.LogError(
                "Refusing PARTIAL_T1 SELL for signal {Id} {Symbol} — entry never filled "
                + "(Status={Status}, BrokerOrderStatus={BrokerStatus}).",
                signal.Id, signal.Symbol, signal.Status, signal.BrokerOrderStatus);
            return;
        }

        // Same rule as the full close: only a stop that could still fill may hold up the partial.
        var cancelResult = await _stopLossSync.CancelBeforeMarketExitAsync(signal, orders, orderRepo, uow);
        if (cancelResult is StopLossCancelResult.AlreadyFilled or StopLossCancelResult.StillResting)
        {
            return;
        }

        var initialQty  = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;
        var closeLots   = Math.Max((int)Math.Ceiling(initialQty * 0.50m), 1);

        // Dropped in the move: a `lotSize` lookup and an `entryOrder` DB query that were both
        // read only by the commented-out journal block this method used to carry. Neither value
        // reached any live code path — TradeCompletionService.CompletePartialExitAsync writes the
        // partial journal once BrokerOrderSyncWorker confirms the fill. Removing the entryOrder
        // lookup also removes one DB round-trip per partial exit.
        var partialOrder = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = Guid.NewGuid().ToString("N")[..20],
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "SELL",
            OrderType = "MARKET",
            OrderPurpose = Trading.Domain.OrderPurpose.PartialExit,
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Quantity = closeLots,
            // The premium the exit decision was made on. A MARKET order ignores it at the broker;
            // PaperOrderStrategy fills at it (it used the last option-chain snapshot, up to ~30 s
            // stale: signal 101's T1 half booked at 266.55 while the premium was above T1 280.06).
            // Also the reference for measuring live slippage (Price vs AvgPrice).
            Price = optionLtp,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await orderRepo.StageAsync(partialOrder);
        await uow.SaveChangesAsync();

        string partialOrderId;
        try { partialOrderId = await orders.ClosePositionAsync(signal, exitReason, closeLots, partialOrder.OrderReferenceId); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Partial close FAILED for signal {Id}", signal.Id);
            partialOrder.Status = OrderStatus.Failed;
            partialOrder.StatusMessage = ex.Message;
            partialOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return;
        }

        if (partialOrderId == "CLOSE_FAILED")
        {
            _logger.LogError("Broker rejected partial close for signal {Id}", signal.Id);
            partialOrder.Status = OrderStatus.Failed;
            partialOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return;
        }

        partialOrder.BrokerOrderId = partialOrderId;
        partialOrder.Status = OrderStatus.Pending;
        partialOrder.UpdatedAt = IstClock.Now;

        // mark waiting for broker confirmation
        signal.Status = SignalStatus.PartialExitPending;

        await uow.SaveChangesAsync();
    }

    /// <summary>The pure guard behind <see cref="HandlePartialExitAsync"/>. Exposed for testing.</summary>
    internal static bool ShouldPlacePartial(int stage, int completedPartials, bool partialInFlight)
        => !partialInFlight && completedPartials < stage;

    // ── Exit reason → OrderPurpose ────────────────────────────────────────────────────────────
    private static string MapExitReasonToPurpose(string exitReason) => exitReason switch
    {
        TradingConst.ExitReason.OptionT1Hit or TradingConst.ExitReason.OptionT2Hit
            or TradingConst.ExitReason.PartialT2 or TradingConst.ExitReason.PartialT3 or TradingConst.ExitReason.OptionT5Hit => Trading.Domain.OrderPurpose.Target,
        TradingConst.ExitReason.ForceExitEod => Trading.Domain.OrderPurpose.ForceExit,
        TradingConst.ExitReason.OptionSlHit or TradingConst.ExitReason.TrailingSlHit => Trading.Domain.OrderPurpose.StopLoss,
        _ => Trading.Domain.OrderPurpose.Exit, // HeroZero / IndexSlHit / PROTECTED_SL_HIT
    };
}
