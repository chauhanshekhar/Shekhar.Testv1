using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

/// <summary>
/// Step 5 of the signal pipeline: persist the approved Signal, write the Order row, place it at
/// the broker, and broadcast. Extracted verbatim from SignalWorker so the worker is the pipeline
/// sequence only — no decision, ordering, or error-handling rule changed in the move.
///
/// The row-before-broker-call ordering and the three distinct broker outcomes (ambiguous /
/// rejected / accepted-then-local-save-failed) are the crash-safety contract with
/// BrokerOrderSyncWorker and must stay exactly as they are: an order row always exists before
/// Groww is called, and a confirmed BrokerOrderId is never downgraded to a rejection.
/// </summary>
public class SignalOrderPlacementService
{
    private readonly ITradingAlertService _alerts;

    public SignalOrderPlacementService(ITradingAlertService alerts)
    {
        _alerts = alerts;
    }

    /// <summary>
    /// RSI stamped on the signal. Both columns hold the 5-minute RSI — the value the CALL/PUT entry
    /// band is checked against (user decision 2026-09-30, to stop the 1m/5m mix-up in the Signals
    /// table). RsiAtEntry is NOT NULL, so when no 5-minute RSI exists yet (fewer than 15 closed
    /// 5-minute bars) it falls back to the 1-minute RSI and Rsi5mAtEntry stays NULL, which marks it.
    /// The 1-minute RSI remains available in IndicatorSnapshots.Rsi.
    /// </summary>
    internal static (decimal RsiAtEntry, decimal? Rsi5mAtEntry) EntryRsi(MarketSnapshot market)
        => (market.Rsi5m ?? market.Rsi, market.Rsi5m);

    public async Task PersistAndPlaceAsync(SignalContext ctx, MarketSnapshot market, Signal signal, string symbol, CancellationToken ct)
    {
        var maxLots = ctx.Settings.MaxLotSize;
        if (maxLots > 0 && signal.Quantity > maxLots)
            signal.Quantity = maxLots;

        // Capture market context at entry for journal analytics
        (signal.RsiAtEntry, signal.Rsi5mAtEntry) = EntryRsi(market);
        signal.PcrAtEntry   = market.PcrValue;
        signal.PcrFullChainAtEntry = market.Audit?.PcrFullChain;

        var entryLeg = SignalAuditService.FindEntryLeg(market.OptionChain, signal);
        signal.DeltaAtEntry      = entryLeg?.Delta      ?? 0m;
        signal.ChangeInOiAtEntry = entryLeg?.ChangeInOi ?? 0m;
        signal.AtrPctAtEntry     = market.AtrPct;
        signal.AtrRawAtEntry     = market.AtrRaw;

        signal.Status = SignalStatus.Pending;
        signal.OrderReferenceId = Guid.NewGuid().ToString("N")[..20];
        await ctx.SignalRepo.StageAsync(signal);
        await ctx.Uow.SaveChangesAsync();

        // Record the order type the broker will actually receive. LiveOrderStrategy reads the same
        // per-symbol flag, so this row never claims LIMIT while a MARKET order was sent.
        var useMarketEntry = ctx.StrategySettings.GetBool(
            LiveOrderStrategy.UseMarketEntryOrdersKey, symbol, false);

        // Order row inserted and saved BEFORE the broker call — if the process crashes or
        // the call times out between here and Groww's response, the order is still visible
        // (Status=PENDING) instead of lost. BrokerOrderSyncWorker reconciles it later via
        // OrderReferenceId.
        var order = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = signal.OrderReferenceId,
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "BUY",
            OrderType = useMarketEntry ? "MARKET" : "LIMIT",
            OrderPurpose = Trading.Domain.OrderPurpose.Entry,
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Price = useMarketEntry ? 0m : (signal.OptionLtp > 0 ? signal.OptionLtp : signal.EntryPrice),
            Quantity = signal.Quantity,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await ctx.OrderRepo.StageAsync(order);
        await ctx.Uow.SaveChangesAsync();

        await PlaceOrderAsync(ctx, signal, order, symbol);

        try { await ctx.Hub.SendSignalAsync(signal); }
        catch (Exception) { }
    }

    // ── Broker order placement ───────────────────────────────────────────────
    private async Task PlaceOrderAsync(SignalContext ctx, Signal signal, Order order, string symbol)
    {
        string orderId;
        try
        {
            orderId = await ctx.Orders.PlaceOrderAsync(signal);
        }
        catch (GrowwOrderAmbiguousException ex)
        {
            // Timeout/network/5xx — Groww may have processed this anyway. Do NOT mark
            // rejected: leave Order=ORDER_UNKNOWN, Signal=PENDING for BrokerOrderSyncWorker
            // to reconcile via OrderReferenceId.
            order.Status = OrderStatus.OrderUnknown;
            order.StatusMessage = ex.Message;
            order.UpdatedAt = IstClock.Now;
            await ctx.Uow.SaveChangesAsync();
            await _alerts.SendCriticalAsync(
                $"Order placement UNKNOWN — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}\nWill reconcile via OrderReferenceId.");
            return;
        }
        catch (Exception ex)
        {
            // Broker call itself failed — no orderId was ever obtained. Genuine rejection.
            signal.Status = SignalStatus.OrderRejected;

            order.Status = OrderStatus.Failed;
            order.StatusMessage = ex.Message;
            order.UpdatedAt = IstClock.Now;

            await ctx.Uow.SaveChangesAsync();
            await _alerts.SendCriticalAsync(
                $"Order rejected — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}");
            return;
        }

        // Groww confirmed the order — from here on any failure is OUR failure, not a broker
        // rejection. Never mark OrderRejected or cancel once we hold a real BrokerOrderId.
        signal.BrokerOrderId = orderId;
        signal.Status = SignalStatus.OrderPlaced;

        // Fill status isn't known yet. Order.Status stays PENDING (non-terminal) so
        // BrokerOrderSyncWorker polls GetOrderStatusAsync (now that BrokerOrderId is set)
        // and fills in the real Status/FilledQty/AvgPrice.
        order.BrokerOrderId = orderId;
        order.UpdatedAt = IstClock.Now;

        try
        {
            await ctx.Uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // DB save failed AFTER the broker accepted the order — the position is real.
            // Leave BrokerOrderId set and defer to BrokerOrderSyncWorker for reconciliation
            // instead of mislabeling this as a rejection.
            order.Status = OrderStatus.OrderUnknown;
            order.StatusMessage = ex.Message;
            await _alerts.SendCriticalAsync(
                $"Order placed at broker (OrderId={orderId}) but local save FAILED — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}\nWill reconcile via BrokerOrderId.");
        }
    }
}
