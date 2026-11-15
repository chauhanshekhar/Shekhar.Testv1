using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Services;

namespace Trading.Worker;

/// <summary>
/// Keeps the Groww access token current so nobody has to paste one in each morning.
///
/// Groww expires the token daily at 06:00 IST. This worker obtains a replacement just after that,
/// and once at startup, via the TOTP flow (GrowwTokenService) — the only Groww auth flow that
/// needs no daily human approval.
///
/// RATE LIMIT: /v1/token/api/access allows 150 requests per 24 hours. This schedule uses one per
/// day plus one per restart, so the budget is never a concern. It deliberately does NOT refresh
/// on a timer or per request.
///
/// SAFE WHEN UNCONFIGURED: with Groww:ApiKey / Groww:TotpSecret absent the worker logs once and
/// idles, and the manually supplied token continues to be used exactly as before. It never
/// overwrites a working token with a failed refresh.
/// </summary>
public class GrowwTokenRefreshWorker : BackgroundService
{
    /// <summary>Groww expires tokens at 06:00 IST; refresh just after so the new one is ready.</summary>
    private static readonly TimeSpan RefreshAt = new(6, 10, 0);

    /// <summary>
    /// Both documented programmatic flows require the API key to be APPROVED on the Groww Cloud
    /// API Keys page each day, so the 06:10 attempt will fail whenever approval has not been given
    /// yet. Retrying means the token is picked up within minutes of you approving, instead of the
    /// system sitting on an expired token until tomorrow.
    ///
    /// Budget: Groww allows 150 token requests per 24h. 15-minute retries capped at 24 attempts
    /// uses at most 25 per day including the scheduled one — comfortably inside it.
    /// </summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(15);
    private const int MaxRetriesPerDay = 24;

    private readonly ILogger<GrowwTokenRefreshWorker> _logger;
    private readonly GrowwTokenService _tokenService;
    private readonly IGrowwAuthService _auth;
    private readonly AppErrorLogger _errorLogger;

    public GrowwTokenRefreshWorker(
        ILogger<GrowwTokenRefreshWorker> logger,
        GrowwTokenService tokenService,
        IGrowwAuthService auth,
        AppErrorLogger errorLogger)
    {
        _logger = logger;
        _tokenService = tokenService;
        _auth = auth;
        _errorLogger = errorLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_tokenService.IsConfigured)
        {
            _logger.LogInformation(
                "Groww automatic token refresh is OFF (Groww:ApiKey / Groww:TotpSecret not set). " +
                "The manually supplied token will continue to be used.");
            return;
        }

        // Refresh at startup: a container that comes up after 06:00 would otherwise run on a
        // token that expired while it was down.
        await RefreshWithRetriesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRefresh();
            _logger.LogInformation(
                "Next Groww token refresh in {Hours:F1}h (at {At} IST)", delay.TotalHours, RefreshAt);

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await RefreshWithRetriesAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Attempts the refresh, then keeps retrying on a fixed interval until it succeeds or the
    /// daily cap is reached. Stops immediately on success.
    /// </summary>
    private async Task RefreshWithRetriesAsync(CancellationToken ct)
    {
        if (await RefreshAsync(ct)) return;

        for (var attempt = 1; attempt <= MaxRetriesPerDay && !ct.IsCancellationRequested; attempt++)
        {
            _logger.LogWarning(
                "Groww token refresh attempt {Attempt}/{Max} failed — retrying in {Minutes} minutes. " +
                "If the API key needs approving on the Groww Cloud API Keys page, approve it and this will pick it up.",
                attempt, MaxRetriesPerDay, RetryInterval.TotalMinutes);

            try { await Task.Delay(RetryInterval, ct); }
            catch (OperationCanceledException) { return; }

            if (await RefreshAsync(ct)) return;
        }

        _logger.LogError(
            "Groww token refresh gave up after {Max} attempts. A token must be supplied manually today.",
            MaxRetriesPerDay);
    }

    /// <summary>Returns true when a new token was obtained and stored.</summary>
    private async Task<bool> RefreshAsync(CancellationToken ct)
    {
        try
        {
            var token = await _tokenService.RequestTokenAsync(ct);

            if (token?.Token == null)
            {
                // Deliberately NOT clearing the existing token — a failed refresh must not take
                // broker access away. The alert makes it visible so it can be pasted in manually.
                _logger.LogError("Groww token refresh FAILED — keeping the existing token");
                await _errorLogger.LogAsync(
                    "GrowwTokenRefreshWorker",
                    new InvalidOperationException("Automatic Groww token refresh failed; existing token retained"),
                    severity: "CRITICAL");
                return false;
            }

            await _auth.UpdateAccessTokenAsync(token.Token, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;   // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groww token refresh threw");
            await _errorLogger.LogAsync("GrowwTokenRefreshWorker", ex, severity: "CRITICAL");
            return false;
        }
    }

    /// <summary>Time until the next 06:10 IST.</summary>
    private static TimeSpan TimeUntilNextRefresh()
    {
        var now = IstClock.Now;
        var next = now.Date.Add(RefreshAt);
        if (now >= next) next = next.AddDays(1);
        return next - now;
    }
}
