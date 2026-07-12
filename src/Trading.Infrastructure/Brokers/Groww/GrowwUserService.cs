using Trading.Application.Models;
using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Brokers.Groww;

// Fetches user/account details from Groww Trade API.
public class GrowwUserService : IGrowwUserService
{
    private readonly IGrowwHttpService _growwHttp;

    public GrowwUserService(IGrowwHttpService growwHttp)
    {
        _growwHttp = growwHttp;
    }

    public async Task<UserDetailPayload?> GetUserDetailAsync()
    {
        var result = await _growwHttp.GetAsync<GrowwResponse<UserDetailPayload>>(GrowwEndpoint.UserDetail);
        return result.Data?.Payload;
    }
}
