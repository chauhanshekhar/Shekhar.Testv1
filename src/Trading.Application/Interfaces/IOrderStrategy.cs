using Trading.Application.Models;
using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

// Strategy interface: each implementation decides HOW orders are executed.
// PaperOrderStrategy  → persists to PaperTrades table (paper mode)
// LiveOrderStrategy   → calls Groww broker API  (live mode)
public interface IOrderStrategy
{
    Task<string> PlaceOrderAsync(Signal signal);
    Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null, string? orderReferenceId = null);

    /// <summary>
    /// Places a broker-managed SL_M (stop-loss market) sell order that protects an open
    /// SINGLE/HERO_ZERO position. Returns the broker order id, or "SL_FAILED" on rejection
    /// (mirrors ClosePositionAsync's "CLOSE_FAILED" convention).
    /// </summary>
    Task<string> PlaceStopLossOrderAsync(Signal signal, decimal triggerPrice, int lots, string orderReferenceId);

    Task<bool> CancelOrderAsync(string orderId);
    Task<bool> ModifyOrderAsync(string orderId, decimal price);
    Task<List<string>> GetOpenOrdersAsync();

    /// <summary>Returns null if the order is not found on the broker.</summary>
    Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId);
}
