using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

public interface IGrowwAuthService
{
    string GetAccessToken();

    /// <summary>
    /// Groww API key, used as the Bearer on the token-generation call.
    /// Resolution order: SystemSettings row -> appsettings/environment -> empty.
    /// </summary>
    string GetApiKey();

    /// <summary>
    /// Base32 TOTP secret used to generate the daily access token.
    /// Resolution order: SystemSettings row -> appsettings/environment -> empty.
    /// </summary>
    string GetTotpSecret();

    /// <summary>
    /// Groww API secret, used by the checksum ("approval") token flow.
    /// Resolution order: SystemSettings row -> appsettings/environment -> empty.
    /// </summary>
    string GetApiSecret();
    Task UpdateAccessTokenAsync(string token, CancellationToken ct = default);
    TradingMode GetMode();
}
