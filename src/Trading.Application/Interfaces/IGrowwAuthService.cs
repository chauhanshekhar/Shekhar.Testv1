using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

public interface IGrowwAuthService
{
    string GetAccessToken();
    Task UpdateAccessTokenAsync(string token, CancellationToken ct = default);
    TradingMode GetMode();
}
