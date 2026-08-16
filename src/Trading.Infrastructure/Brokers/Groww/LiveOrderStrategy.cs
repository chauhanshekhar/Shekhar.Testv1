using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// Strategy: live trading — orders sent to Groww broker API.
/// Template Method: subclasses override <see cref="GetLots"/> to control quantity.
/// </summary>
public class LiveOrderStrategy : IOrderStrategy
{
    private readonly IGrowwHttpService _growwHttp;
    private readonly IInstrumentCache _instruments;

    public LiveOrderStrategy(IGrowwHttpService growwHttp, IInstrumentCache instruments)
    {
        _growwHttp = growwHttp;
        _instruments = instruments;
    }

    // ── Template Method — override in HeroZeroOrderStrategy ──────────────────
    protected virtual int GetLots(Signal signal) => signal.Quantity > 0 ? signal.Quantity : 1;

    private int GetLotSize(string symbol) => _instruments.GetLotSize(symbol);

    // ── IOrderStrategy ────────────────────────────────────────────────────────

    public async Task<string> PlaceOrderAsync(Signal signal)
    {
        var contracts = GetLots(signal) * GetLotSize(signal.Symbol);
        var orderReferenceId = signal.OrderReferenceId;
        var signalId = signal.Id.ToString();

        // Leg 1 (always a buy for CE/PE ATM option)
        var req1 = new PlaceOrderRequestBuilder().FromSignalEntry(signal, contracts, orderReferenceId).Build();
        var result1 = await _growwHttp.PostAsync<GrowwResponse<PlaceOrderResponse>>(
            GrowwEndpoint.OrderCreate, req1, signalId, orderReferenceId);

        if (!result1.IsSuccess)
        {
            if (result1.IsAmbiguous)
                throw new GrowwOrderAmbiguousException(result1.ErrorMessage ?? "Groww order-create timed out or failed ambiguously");
            throw new GrowwOrderRejectedException(result1.ErrorMessage ?? "Groww rejected the order");
        }

        if (result1.Data?.IsSuccess != true)
            throw new GrowwOrderRejectedException(result1.Data?.ErrorMessage ?? "Groww rejected the order");

        var orderId1 = result1.Data?.Payload?.GrowwOrderId;
        if (string.IsNullOrEmpty(orderId1))
            throw new GrowwOrderAmbiguousException("Groww returned SUCCESS with no order id");

        // Leg 2 for spreads (short OTM)
        if (signal.StrategyType != "SINGLE" && !string.IsNullOrEmpty(signal.Leg2Symbol))
        {
            var req2 = new PlaceOrderRequestBuilder().FromSpreadShortEntry(signal, contracts).Build();
            var result2 = await _growwHttp.PostAsync<GrowwResponse<PlaceOrderResponse>>(
                GrowwEndpoint.OrderCreate, req2, signalId, orderReferenceId);
            if (result2.IsSuccess)
            {
                signal.Leg2OrderId = result2.Data?.Payload?.GrowwOrderId ?? "";
            }
            return $"{orderId1},{signal.Leg2OrderId}";
        }

        return orderId1;
    }

    public async Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null, string? orderReferenceId = null)
    {
        // Partial close passes an explicit lot count. Full close uses remaining open lots.
        var closeLots = lotsToClose ?? signal.RemainingQty;
        if (closeLots <= 0)
            return "CLOSE_NOTHING";

        var closeContracts = closeLots * GetLotSize(signal.Symbol);
        var signalId = signal.Id.ToString();

        var req1 = new PlaceOrderRequestBuilder().FromSignalExit(signal, closeContracts, orderReferenceId).Build();
        var result1 = await _growwHttp.PostAsync<GrowwResponse<PlaceOrderResponse>>(
            GrowwEndpoint.OrderCreate, req1, signalId, orderReferenceId);
        if (!result1.IsSuccess || result1.Data?.IsSuccess != true) return "CLOSE_FAILED";
        var orderId1 = result1.Data?.Payload?.GrowwOrderId;
        if (string.IsNullOrEmpty(orderId1)) return "CLOSE_FAILED";

        if (signal.StrategyType != "SINGLE"
           && !string.IsNullOrEmpty(signal.Leg2Symbol)
           && !string.IsNullOrEmpty(signal.Leg2OrderId))
        {
            var req2 = new PlaceOrderRequestBuilder()
                .FromSpreadShortExit(signal, closeContracts)
                .Build();

            var result2 = await _growwHttp.PostAsync<GrowwResponse<PlaceOrderResponse>>(
                GrowwEndpoint.OrderCreate, req2, signalId);

            if (result2.IsSuccess)
                return $"{orderId1},{result2.Data?.Payload?.GrowwOrderId}";
        }
        return orderId1;
    }

    public async Task<string> PlaceStopLossOrderAsync(Signal signal, decimal triggerPrice, int lots, string orderReferenceId)
    {
        var contracts = lots * GetLotSize(signal.Symbol);
        var signalId = signal.Id.ToString();

        var req = new PlaceOrderRequestBuilder().FromSignalStopLoss(signal, contracts, triggerPrice, orderReferenceId).Build();
        var result = await _growwHttp.PostAsync<GrowwResponse<PlaceOrderResponse>>(
            GrowwEndpoint.OrderCreate, req, signalId, orderReferenceId);

        if (!result.IsSuccess || result.Data?.IsSuccess != true) return "SL_FAILED";

        var orderId = result.Data?.Payload?.GrowwOrderId;
        return string.IsNullOrEmpty(orderId) ? "SL_FAILED" : orderId;
    }

    public async Task<bool> CancelOrderAsync(string orderId)
    {
        var request = new CancelOrderRequest { GrowwOrderId = orderId, Segment = GrowwSegment.Fno };
        var result = await _growwHttp.PostAsync<GrowwResponse<object>>(GrowwEndpoint.OrderCancel, request);
        if (result.IsSuccess) return true;

        // Some brokers report "already cancelled" as a failure response rather than a no-op
        // success — treat it as a successful (idempotent) cancel, since the order is provably
        // no longer live. Deliberately does NOT match "already complete/executed" — that case
        // means the order filled, not that it's safely gone, and callers (StopLossOrderCoordinator)
        // need that distinction to avoid placing a duplicate SELL on top of a filled order.
        var errorText = $"{result.ErrorMessage} {result.Data?.ErrorMessage} {result.Data?.Error?.Message}";
        return errorText.Contains("ALREADY", StringComparison.OrdinalIgnoreCase)
            && errorText.Contains("CANCEL", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> ModifyOrderAsync(string orderId, decimal price)
    {
        var request = new ModifyOrderRequest { GrowwOrderId = orderId, Segment = GrowwSegment.Fno, OrderType = GrowwOrderType.Limit, Price = price };
        var result = await _growwHttp.PostAsync<GrowwResponse<object>>(GrowwEndpoint.OrderModify, request);
        return result.IsSuccess;
    }

    public async Task<List<string>> GetOpenOrdersAsync()
    {
        var result = await _growwHttp.GetAsync<GrowwResponse<OrderListPayload>>($"{GrowwEndpoint.OrderList}?segment=FNO");
        return result.Data?.Payload?.Orders
            .Where(o => o.OrderStatus is "PLACED" or "OPEN" or "TRIGGER PENDING")
            .Select(o => o.GrowwOrderId)
            .ToList() ?? [];
    }

    public async Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId)
    {
        var detail = await GetOrderDetailAsync(brokerOrderId);
        if (detail is null) return null;

        return new BrokerOrderStatus
        {
            Status        = NormalizeGrowwStatus(detail.OrderStatus ?? ""),
            StatusMessage = detail.Remark ?? "",
            FilledQty     = detail.FilledQuantity ?? 0,
            AvgPrice      = detail.AverageFillPrice ?? 0m,
        };
    }

    private static string NormalizeGrowwStatus(string raw) => raw.ToUpperInvariant() switch
    {
        "COMPLETE" or "COMPLETED" or "EXECUTED"                    => "COMPLETE",
        "REJECTED"                                                 => "REJECTED",
        "CANCELLED"                                                => "CANCELLED",
        "OPEN"                                                     => "OPEN",
        "PLACED" or "NEW" or "ACKED" or "APPROVED"
            or "TRIGGER_PENDING" or "TRIGGER PENDING"               => "PENDING",
        _                                                          => "PENDING",
    };

    // ── Extended methods (detail, trades) ────────────────────────────────────

    public async Task<OrderDetail?> GetOrderDetailAsync(string growwOrderId, string segment = GrowwSegment.Fno)
    {
        var url = $"{string.Format(GrowwEndpoint.OrderDetail, Uri.EscapeDataString(growwOrderId))}?segment={segment}";
        var result = await _growwHttp.GetAsync<GrowwResponse<OrderDetail>>(url);
        return result.Data?.Payload;
    }

    public async Task<OrderDetail?> GetOrderByReferenceAsync(string referenceId, string segment = GrowwSegment.Fno)
    {
        var url = $"{string.Format(GrowwEndpoint.OrderByReference, Uri.EscapeDataString(referenceId))}?segment={segment}";
        var result = await _growwHttp.GetAsync<GrowwResponse<OrderDetail>>(url);
        return result.Data?.Payload;
    }

    public async Task<List<TradeDetail>> GetTradesForOrderAsync(
        string growwOrderId,
        string segment = GrowwSegment.Fno,
        int page = 0,
        int pageSize = 20)
    {
        var url = $"{string.Format(GrowwEndpoint.OrderTrades, Uri.EscapeDataString(growwOrderId))}?segment={segment}&page={page}&page_size={pageSize}";
        var result = await _growwHttp.GetAsync<GrowwResponse<TradeListPayload>>(url);
        return result.Data?.Payload?.Trades ?? [];
    }
}
