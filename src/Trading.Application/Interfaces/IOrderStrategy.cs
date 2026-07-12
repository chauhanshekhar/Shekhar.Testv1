using Trading.Application.Models;
using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

// Strategy interface: each implementation decides HOW orders are executed.
// PaperOrderStrategy  → persists to PaperTrades table (paper mode)
// LiveOrderStrategy   → calls Groww broker API  (live mode)
public interface IOrderStrategy
{
    Task<string> PlaceOrderAsync(Signal signal);
    Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null);
    Task<bool> CancelOrderAsync(string orderId);
    Task<bool> ModifyOrderAsync(string orderId, decimal price);
    Task<List<string>> GetOpenOrdersAsync();

    /// <summary>Returns null if the order is not found on the broker.</summary>
    Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId);
}
