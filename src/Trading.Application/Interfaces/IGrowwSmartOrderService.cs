using Trading.Application.Models;

namespace Trading.Application.Interfaces;

public interface IGrowwSmartOrderService
{
    Task<SmartOrderDetail?> CreateGttAsync(CreateGttRequest request);
    Task<SmartOrderDetail?> CreateOcoAsync(CreateOcoRequest request);
    Task<SmartOrderDetail?> ModifyGttAsync(string smartOrderId, ModifyGttRequest request);
    Task<SmartOrderDetail?> ModifyOcoAsync(string smartOrderId, ModifyOcoRequest request);
    Task<bool> CancelSmartOrderAsync(string segment, string smartOrderType, string smartOrderId);
    Task<SmartOrderDetail?> GetSmartOrderAsync(string segment, string smartOrderType, string smartOrderId);
    Task<List<SmartOrderDetail>> ListSmartOrdersAsync(
        string? segment = null,
        string smartOrderType = "OCO",
        string status = "ACTIVE",
        int page = 0,
        int pageSize = 20,
        string? startDateTime = null,
        string? endDateTime = null);
}
