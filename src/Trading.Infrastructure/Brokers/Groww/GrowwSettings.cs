using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

public class GrowwSettings
{
    /// <summary>
    /// Manually pasted token. Still honoured, and still the fallback when automatic refresh is
    /// not configured or fails.
    /// </summary>
    public string AccessToken { get; set; } = "";

    // ── Automatic daily token refresh (TOTP flow) ────────────────────────────────────────────
    // Both are CREDENTIALS. Supply them via environment variables or user-secrets — never commit
    // them. Env var form: Groww__ApiKey and Groww__TotpSecret.
    //
    // With both set, GrowwTokenService obtains a token from POST /v1/token/api/access and nobody
    // has to paste one in each morning. With either missing, behaviour is exactly as before.

    /// <summary>API key from the Groww Cloud API Keys page. Sent as the Bearer on the token call.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Base32 TOTP secret the authenticator app was enrolled with. A-Z and 2-7 ONLY.
    /// This is NOT the API secret — see ApiSecret below.
    /// </summary>
    public string TotpSecret { get; set; } = "";

    /// <summary>
    /// API secret from the Cloud API Keys page, used by the checksum ("approval") flow:
    /// checksum = SHA-256(apiSecret + epochSeconds). Distinct from TotpSecret.
    /// </summary>
    public string ApiSecret { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.groww.in";
    public TradingMode Mode { get; set; } = TradingMode.Paper;

    public bool IsPaperMode => Mode == TradingMode.Paper;
    public bool IsHeroZeroMode => Mode == TradingMode.HeroZero;
    public bool IsLiveMode => Mode == TradingMode.Live;
}
