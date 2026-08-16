using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// Routes order execution to the correct strategy based on <see cref="GrowwSettings.Mode"/>:
///   Paper     → PaperOrderStrategy     (no broker API, DB only)
///   Live      → LiveOrderStrategy      (Groww API, full lot size)
///   HeroZero  → HeroZeroOrderStrategy  (Groww API, forced 1 lot)
/// </summary>
public class OrderRouter : IOrderService
{
    private readonly IOrderStrategy _paper;
    private readonly IOrderStrategy _live;
    private readonly IOrderStrategy _heroZero;
    private readonly LiveOrderStrategy _liveStrategy; // for extended Groww methods
    private readonly IGrowwAuthService _auth;
    public TradingMode CurrentMode =>
    _auth.GetMode();

    public OrderRouter(
        PaperOrderStrategy paper,
        LiveOrderStrategy live,
        HeroZeroOrderStrategy heroZero,
        IGrowwAuthService auth)
    {
        _paper = paper;
        _live = live;
        _heroZero = heroZero;
        _liveStrategy = live;
        _auth = auth;
    }

    private IOrderStrategy Strategy => _auth.GetMode() switch
    {
        TradingMode.Paper => _paper,
        TradingMode.HeroZero => _heroZero,
        _ => _live,   // Live (default)
    };

    public Task<string> PlaceOrderAsync(Signal signal) => Strategy.PlaceOrderAsync(signal);
    public Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null, string? orderReferenceId = null) => Strategy.ClosePositionAsync(signal, reason, lotsToClose, orderReferenceId);
    public Task<string> PlaceStopLossOrderAsync(Signal signal, decimal triggerPrice, int lots, string orderReferenceId) => Strategy.PlaceStopLossOrderAsync(signal, triggerPrice, lots, orderReferenceId);
    public Task<bool> CancelOrderAsync(string orderId) => Strategy.CancelOrderAsync(orderId);
    public Task<bool> ModifyOrderAsync(string orderId, decimal price) => Strategy.ModifyOrderAsync(orderId, price);
    public Task<List<string>> GetOpenOrdersAsync() => Strategy.GetOpenOrdersAsync();
    public Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId) => Strategy.GetOrderStatusAsync(brokerOrderId);

    // ── Extended Groww-only methods (live / hero-zero, not paper) ─────────────

    public Task<OrderDetail?> GetOrderDetailAsync(string growwOrderId, string segment = GrowwSegment.Fno)
        => _liveStrategy.GetOrderDetailAsync(growwOrderId, segment);

    public Task<OrderDetail?> GetOrderByReferenceAsync(string referenceId, string segment = GrowwSegment.Fno)
        => _liveStrategy.GetOrderByReferenceAsync(referenceId, segment);

    public Task<List<TradeDetail>> GetTradesForOrderAsync(string growwOrderId, string segment = GrowwSegment.Fno, int page = 0, int pageSize = 20)
        => _liveStrategy.GetTradesForOrderAsync(growwOrderId, segment, page, pageSize);
}
