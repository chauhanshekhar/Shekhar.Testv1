using Trading.Application.Models;
using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Brokers.Groww;

// GTT (Good Till Triggered) and OCO (One Cancels Other) smart orders via Groww Trade API.
public class GrowwSmartOrderService : IGrowwSmartOrderService
{
    private readonly IGrowwHttpService _growwHttp;

    public GrowwSmartOrderService(IGrowwHttpService growwHttp)
    {
        _growwHttp = growwHttp;
    }

    public async Task<SmartOrderDetail?> CreateGttAsync(CreateGttRequest request)
    {
        var result = await _growwHttp.PostAsync<GrowwResponse<SmartOrderDetail>>(GrowwEndpoint.SmartOrderCreate, request);
        return result.Data?.Payload;
    }

    public async Task<SmartOrderDetail?> CreateOcoAsync(CreateOcoRequest request)
    {
        var result = await _growwHttp.PostAsync<GrowwResponse<SmartOrderDetail>>(GrowwEndpoint.SmartOrderCreate, request);
        return result.Data?.Payload;
    }

    public async Task<SmartOrderDetail?> ModifyGttAsync(string smartOrderId, ModifyGttRequest request)
    {
        var result = await _growwHttp.PutAsync<GrowwResponse<SmartOrderDetail>>(
            string.Format(GrowwEndpoint.SmartOrderModify, smartOrderId), request);
        return result.Data?.Payload;
    }

    public async Task<SmartOrderDetail?> ModifyOcoAsync(string smartOrderId, ModifyOcoRequest request)
    {
        var result = await _growwHttp.PutAsync<GrowwResponse<SmartOrderDetail>>(
            string.Format(GrowwEndpoint.SmartOrderModify, smartOrderId), request);
        return result.Data?.Payload;
    }

    public async Task<bool> CancelSmartOrderAsync(
        string segment,
        string smartOrderType,
        string smartOrderId)
    {
        var result = await _growwHttp.PostAsync<object>(
            string.Format(GrowwEndpoint.SmartOrderCancel, segment, smartOrderType, smartOrderId),
            new { });
        return result.IsSuccess;
    }

    public async Task<SmartOrderDetail?> GetSmartOrderAsync(
        string segment,
        string smartOrderType,
        string smartOrderId)
    {
        var result = await _growwHttp.GetAsync<GrowwResponse<SmartOrderDetail>>(
            string.Format(GrowwEndpoint.SmartOrderStatus, segment, smartOrderType, smartOrderId));
        return result.Data?.Payload;
    }

    public async Task<List<SmartOrderDetail>> ListSmartOrdersAsync(
        string? segment = null,
        string smartOrderType = GrowwSmartOrderType.Oco,
        string status = "ACTIVE",
        int page = 0,
        int pageSize = 20,
        string? startDateTime = null,
        string? endDateTime = null)
    {
        var url = GrowwEndpoint.SmartOrderList +
                  $"?smart_order_type={smartOrderType}" +
                  $"&status={status}" +
                  $"&page={page}" +
                  $"&page_size={pageSize}";

        if (!string.IsNullOrEmpty(segment)) url += $"&segment={segment}";
        if (!string.IsNullOrEmpty(startDateTime)) url += $"&start_date_time={Uri.EscapeDataString(startDateTime)}";
        if (!string.IsNullOrEmpty(endDateTime)) url += $"&end_date_time={Uri.EscapeDataString(endDateTime)}";

        var result = await _growwHttp.GetAsync<GrowwResponse<SmartOrderListPayload>>(url);
        return result.Data?.Payload?.Orders ?? [];
    }
}
