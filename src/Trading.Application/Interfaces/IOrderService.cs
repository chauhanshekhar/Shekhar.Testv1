using Trading.Application.Models;
using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

public interface IOrderService
{
    TradingMode CurrentMode { get; }

    /// <summary>
    /// True when this signal is executed as a paper trade (no broker orders). Defaults to the global
    /// mode; <c>OrderRouter</c> also treats Scalping-state signals as paper in Live/HeroZero mode.
    /// Callers must use this, not <see cref="CurrentMode"/>, to decide whether to place broker stops
    /// or expect broker fills for a signal.
    /// </summary>
    bool IsPaperSignal(Signal signal) => CurrentMode == TradingMode.Paper;
    /// <summary>
    /// Places the entry order for a signal. signal.OrderReferenceId must already be set
    /// (by the caller, before the Order row is inserted) — it's sent to Groww as-is so a
    /// later GetOrderByReferenceAsync can recover an order whose placement response never
    /// arrived. Throws GrowwOrderRejectedException on a definite rejection, or
    /// GrowwOrderAmbiguousException on a timeout/network/5xx where Groww may have processed
    /// the order anyway.
    /// </summary>
    Task<string> PlaceOrderAsync(
        Signal signal);

    Task<bool> CancelOrderAsync(
        string orderId);

    Task<bool> ModifyOrderAsync(
        string orderId,
        decimal price);

    Task<List<string>> GetOpenOrdersAsync();

    /// <summary>
    /// Fetches the current broker status for a single order.
    /// Returns null if the order cannot be found on the broker.
    /// </summary>
    Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId);

    /// <summary>
    /// Closes an open position by placing the opposite order at market. orderReferenceId
    /// should be the caller's Order row's OrderReferenceId — it's sent to Groww as-is so a
    /// later GetOrderByReferenceAsync can recover this specific close order.
    /// </summary>
    Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null, string? orderReferenceId = null);

    /// <summary>
    /// Places a broker-managed SL_M (stop-loss market) sell order that protects an open
    /// SINGLE/HERO_ZERO position. Returns the broker order id, or "SL_FAILED" on rejection
    /// (mirrors ClosePositionAsync's "CLOSE_FAILED" convention).
    /// </summary>
    Task<string> PlaceStopLossOrderAsync(Signal signal, decimal triggerPrice, int lots, string orderReferenceId);

    /// <summary>Looks up an order by Groww's own order id — used once BrokerOrderId is known.</summary>
    Task<OrderDetail?> GetOrderDetailAsync(string growwOrderId, string segment = "FNO");

    /// <summary>
    /// Looks up an order by OUR OrderReferenceId — used for reconciling an order whose
    /// BrokerOrderId was never confirmed (timeout/network failure during placement).
    /// </summary>
    Task<OrderDetail?> GetOrderByReferenceAsync(string referenceId, string segment = "FNO");

    Task<List<TradeDetail>> GetTradesForOrderAsync(string growwOrderId, string segment = "FNO", int page = 0, int pageSize = 20);
}
