using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Sends alerts via Twilio WhatsApp API using plain HttpClient (no Twilio SDK required).
/// Configure WhatsApp:AccountSid, WhatsApp:AuthToken, WhatsApp:From, WhatsApp:To in appsettings.
/// </summary>
public sealed class WhatsAppAlertService : ITradingAlertService
{
    private readonly HttpClient _http;
    private readonly ILogger<WhatsAppAlertService> _logger;
    private readonly string? _apiUrl;
    private readonly string? _from;
    private readonly string? _to;

    public WhatsAppAlertService(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<WhatsAppAlertService> logger)
    {
        _logger = logger;
        _http   = factory.CreateClient();

        var accountSid = config["WhatsApp:AccountSid"];
        var authToken  = config["WhatsApp:AuthToken"];
        _from = config["WhatsApp:From"];
        _to   = config["WhatsApp:To"];

        if (!string.IsNullOrWhiteSpace(accountSid) && !string.IsNullOrWhiteSpace(authToken))
        {
            _apiUrl = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        }
    }

    public Task SendCriticalAsync(string message)   => SendAsync($"⚠️ CRITICAL\n{message}");
    public Task SendTradeAlertAsync(string message) => SendAsync($"📊 TRADE\n{message}");

    private async Task SendAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(_apiUrl) || string.IsNullOrWhiteSpace(_from) || string.IsNullOrWhiteSpace(_to))
            return;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["From"] = _from,
                ["To"]   = _to,
                ["Body"] = text
            });

            var response = await _http.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsAppAlertService: failed to send message");
        }
    }
}
