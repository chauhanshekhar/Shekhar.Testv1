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
    private readonly IStrategySettingsService? _settings;

    /// <summary>StrategySettings key (default on). 0 = Scalping signals follow the global mode again.</summary>
    public const string ScalpingPaperOnlyKey = "ScalpingPaperOnly";
    /// <summary>Market states executed as paper only (gated by <see cref="ScalpingPaperOnlyKey"/>).</summary>
    internal static readonly string[] PaperOnlyStates = { "Scalping", "Reversal" };
    internal const string PaperIdPrefix = "PAPER-";

    public TradingMode CurrentMode =>
    _auth.GetMode();

    public OrderRouter(
        PaperOrderStrategy paper,
        LiveOrderStrategy live,
        HeroZeroOrderStrategy heroZero,
        IGrowwAuthService auth,
        IStrategySettingsService? settings = null)
    {
        _paper = paper;
        _live = live;
        _heroZero = heroZero;
        _liveStrategy = live;
        _auth = auth;
        _settings = settings;
    }

    private IOrderStrategy Strategy => _auth.GetMode() switch
    {
        TradingMode.Paper => _paper,
        TradingMode.HeroZero => _heroZero,
        _ => _live,   // Live (default)
    };

    /// <summary>
    /// Paper vs live for one signal. Paper mode is always paper. Otherwise, once the entry order exists
    /// its id decides (PAPER- = paper, anything else = live) so a position always closes the way it
    /// opened, even across a restart or a setting change. Before any entry order exists (BrokerOrderId
    /// empty) a Scalping-state signal is paper when <paramref name="scalpingPaperOnly"/> is on.
    /// </summary>
    internal static bool RoutesToPaper(TradingMode mode, Signal signal, bool scalpingPaperOnly)
    {
        if (mode == TradingMode.Paper) return true;

        if (!string.IsNullOrEmpty(signal.BrokerOrderId))
            return signal.BrokerOrderId.StartsWith(PaperIdPrefix, StringComparison.Ordinal);

        return scalpingPaperOnly
            && Array.Exists(PaperOnlyStates,
                s => string.Equals(signal.MarketState, s, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsPaperSignal(Signal signal)
        => RoutesToPaper(_auth.GetMode(), signal,
            _settings?.GetBool(ScalpingPaperOnlyKey, signal.Symbol, true) ?? true);

    private IOrderStrategy StrategyFor(Signal signal) => IsPaperSignal(signal) ? _paper : Strategy;

    // Order-id-only calls: a PAPER- id is always handled by the paper strategy.
    private IOrderStrategy StrategyFor(string orderId)
        => orderId.StartsWith(PaperIdPrefix, StringComparison.Ordinal) ? _paper : Strategy;

    public Task<string> PlaceOrderAsync(Signal signal) => StrategyFor(signal).PlaceOrderAsync(signal);
    public Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null, string? orderReferenceId = null) => StrategyFor(signal).ClosePositionAsync(signal, reason, lotsToClose, orderReferenceId);
    public Task<string> PlaceStopLossOrderAsync(Signal signal, decimal triggerPrice, int lots, string orderReferenceId) => StrategyFor(signal).PlaceStopLossOrderAsync(signal, triggerPrice, lots, orderReferenceId);
    public Task<bool> CancelOrderAsync(string orderId) => StrategyFor(orderId).CancelOrderAsync(orderId);
    public Task<bool> ModifyOrderAsync(string orderId, decimal price) => StrategyFor(orderId).ModifyOrderAsync(orderId, price);
    public Task<List<string>> GetOpenOrdersAsync() => Strategy.GetOpenOrdersAsync();
    public Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId) => StrategyFor(brokerOrderId).GetOrderStatusAsync(brokerOrderId);

    // ── Extended Groww-only methods (live / hero-zero, not paper) ─────────────

    public Task<OrderDetail?> GetOrderDetailAsync(string growwOrderId, string segment = GrowwSegment.Fno)
        => _liveStrategy.GetOrderDetailAsync(growwOrderId, segment);

    public Task<OrderDetail?> GetOrderByReferenceAsync(string referenceId, string segment = GrowwSegment.Fno)
        => _liveStrategy.GetOrderByReferenceAsync(referenceId, segment);

    public Task<List<TradeDetail>> GetTradesForOrderAsync(string growwOrderId, string segment = GrowwSegment.Fno, int page = 0, int pageSize = 20)
        => _liveStrategy.GetTradesForOrderAsync(growwOrderId, segment, page, pageSize);
}
