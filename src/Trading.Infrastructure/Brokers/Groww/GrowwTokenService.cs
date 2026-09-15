using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
using Trading.Domain;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>Response of POST /v1/token/api/access.</summary>
public sealed class GrowwTokenResponse
{
    [JsonPropertyName("token")]        public string? Token { get; set; }
    [JsonPropertyName("tokenRefId")]   public string? TokenRefId { get; set; }
    [JsonPropertyName("sessionName")]  public string? SessionName { get; set; }
    [JsonPropertyName("expiry")]       public DateTime? Expiry { get; set; }
    [JsonPropertyName("isActive")]     public bool? IsActive { get; set; }
}

/// <summary>
/// Obtains a Groww access token WITHOUT anyone pasting one in each morning.
///
///     POST {BaseUrl}/v1/token/api/access
///     Authorization: Bearer {ApiKey}
///     { "key_type": "totp", "totp": "&lt;6-digit code&gt;" }
///
/// The TOTP flow is the only fully automatable one. The alternative "approval" flow requires a
/// human to approve on the Groww Cloud API Keys page every day, which defeats the purpose.
///
/// RATE LIMIT: Groww caps /v1/token/api/access at 150 requests per 24 hours. This service is
/// therefore called only when the cached token has actually expired — never per request. At one
/// refresh a day plus restarts, the budget is untouched.
///
/// CONFIGURATION — resolved by IGrowwAuthService, SystemSettings row first then appsettings/env:
///     SystemSettings 'GrowwApiKey'      / Groww:ApiKey       API key from the Cloud API Keys page
///     SystemSettings 'GrowwTotpSecret'  / Groww:TotpSecret   Base32 authenticator secret
///
/// Both are CREDENTIALS. Stored in SystemSettings they are plaintext in the database and in every
/// backup, and any `SELECT * FROM SystemSettings` (or a screenshot of one) exposes them. Restrict
/// read access to that table accordingly, or supply them by environment variable instead.
/// </summary>
public class GrowwTokenService
{
    private const string TokenPath = "/v1/token/api/access";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<GrowwSettings> _options;
    private readonly IGrowwAuthService _auth;
    private readonly ILogger<GrowwTokenService> _logger;

    public GrowwTokenService(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<GrowwSettings> options,
        IGrowwAuthService auth,
        ILogger<GrowwTokenService> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _auth = auth;
        _logger = logger;
    }

    /// <summary>
    /// True when the API key plus EITHER credential is present, so some flow can run.
    /// TOTP is preferred; the checksum flow is used when no usable TOTP secret is configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_auth.GetApiKey())
        && (IsUsableTotpSecret(_auth.GetTotpSecret()) || !string.IsNullOrWhiteSpace(_auth.GetApiSecret()));

    /// <summary>
    /// A TOTP secret must be Base32 — A-Z and 2-7 only. Guards against the common mix-up of
    /// storing the API SECRET here: that string contains symbols and the digits 0/1/8/9, none of
    /// which are Base32, and it would otherwise fail every day with a FormatException.
    /// </summary>
    internal static bool IsUsableTotpSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;

        var cleaned = secret.Replace(" ", "").Replace("-", "").TrimEnd('=');
        return cleaned.Length > 0
               && cleaned.All(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '2' && c <= '7'));
    }

    /// <summary>checksum = SHA-256(apiSecret + epochSeconds), lower-case hex.</summary>
    internal static string ComputeChecksum(string apiSecret, long epochSeconds)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(apiSecret + epochSeconds);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a fresh access token. Returns null on any failure — the caller keeps using
    /// whatever token it already has rather than losing broker access outright.
    /// </summary>
    public async Task<GrowwTokenResponse?> RequestTokenAsync(CancellationToken ct = default)
    {
        var cfg = _options.CurrentValue;
        var apiKey = _auth.GetApiKey();
        // Read only to CHOOSE the flow: a usable Base32 value selects TOTP, anything else
        // (including an absent row) falls through to the checksum flow below.
        var totpSecret = _auth.GetTotpSecret();

        if (!IsConfigured)
        {
            _logger.LogWarning(
                "Groww automatic token refresh is not configured. Needs SystemSettings row 'GrowwApiKey' PLUS either " +
                "'GrowwTotpSecret' (Base32, A-Z and 2-7 only) for the TOTP flow, or 'GrowwApiSecret' for the checksum flow. " +
                "Falling back to the manually supplied token.");
            return null;
        }

        object requestBody;
        string flow;

        if (IsUsableTotpSecret(totpSecret))
        {
            // A code generated in the last seconds of its window can expire in flight. Waiting for
            // the next window costs a few seconds once a day and removes a class of spurious failure.
            var secondsLeft = TotpGenerator.SecondsRemaining(DateTime.UtcNow);
            if (secondsLeft <= 3)
            {
                _logger.LogInformation("TOTP window has {Seconds}s left — waiting for the next one", secondsLeft);
                await Task.Delay(TimeSpan.FromSeconds(secondsLeft + 1), ct);
            }

            try
            {
                requestBody = new { key_type = "totp", totp = TotpGenerator.Generate(totpSecret, DateTime.UtcNow) };
                flow = "totp";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not generate a TOTP code from the configured secret");
                return null;
            }
        }
        else
        {
            var apiSecret = _auth.GetApiSecret();
            if (string.IsNullOrWhiteSpace(apiSecret))
            {
                _logger.LogError(
                    "No usable credential: the TOTP secret is not Base32 (A-Z, 2-7 only — the API SECRET is not a TOTP secret) " +
                    "and no API secret is configured for the checksum flow.");
                return null;
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            requestBody = new
            {
                key_type = "approval",
                checksum = ComputeChecksum(apiSecret, timestamp),
                timestamp = timestamp.ToString()
            };
            flow = "approval";

            _logger.LogInformation(
                "Using the checksum (approval) token flow — no Base32 TOTP secret is configured.");
        }

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{cfg.BaseUrl.TrimEnd('/')}{TokenPath}")
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("X-API-VERSION", "1.0");

            using var response = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Never log the body verbatim — it can echo request material. Status + reason only.
                _logger.LogError(
                    "Groww token request failed via {Flow} flow: HTTP {Status} {Reason}", flow, (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var parsed = System.Text.Json.JsonSerializer.Deserialize<GrowwTokenResponse>(responseBody);

            if (string.IsNullOrWhiteSpace(parsed?.Token))
            {
                _logger.LogError("Groww token request returned HTTP 200 with no token field");
                return null;
            }

            if (parsed.IsActive == false)
            {
                _logger.LogError("Groww returned a token marked isActive=false — not using it");
                return null;
            }

            _logger.LogInformation(
                "Groww access token refreshed automatically. Expiry={Expiry} SessionName={Session}",
                parsed.Expiry, parsed.SessionName);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groww token request threw");
            return null;
        }
    }
}
