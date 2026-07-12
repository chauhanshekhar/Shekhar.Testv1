using Trading.Application.Models;
using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Brokers.Groww;

public class GrowwMarginService : IMarginService
{
    private readonly IGrowwHttpService _growwHttp;
    private readonly IOptionsMonitor<GrowwSettings> _options;

    private GrowwSettings _settings => _options.CurrentValue;

    public GrowwMarginService(
        IGrowwHttpService growwHttp,
        IOptionsMonitor<GrowwSettings> options)
    {
        _growwHttp = growwHttp;
        _options = options;
    }

    public async Task<MarginInfo?> GetMarginAsync()
    {
        if (_settings.IsPaperMode)
            return new MarginInfo(AvailableCash: 500_000m, UsedMargin: 0m, TotalCapital: 500_000m);

        var result = await _growwHttp.GetAsync<GrowwResponse<MarginDetailPayload>>(GrowwEndpoint.MarginUser);

        if (result.Data?.Payload is null)
            return null;

        var available = (result.Data.Payload.ClearCash ?? 0m) + (result.Data.Payload.CollateralAvailable ?? 0m);
        var used = result.Data.Payload.NetMarginUsed ?? 0m;
        return new MarginInfo(available, used, available + used);
    }

    // Calculate margin required for a potential order (SPAN + exposure + premium + brokerage).
    public async Task<OrderMarginPayload?> GetRequiredMarginAsync(OrderMarginRequest request)
    {
        var result = await _growwHttp.PostAsync<GrowwResponse<OrderMarginPayload>>(
            $"{GrowwEndpoint.MarginOrder}?segment={request.Segment}", request);
        return result.Data?.Payload;
    }
}
