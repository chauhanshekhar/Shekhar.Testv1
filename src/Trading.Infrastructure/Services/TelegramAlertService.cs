using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Sends critical alerts to a Telegram chat using the Bot API.
/// Uses plain HttpClient so no extra package dependency is needed.
/// </summary>
public sealed class TelegramAlertService : ITradingAlertService
{
    private readonly HttpClient _http;
    private readonly ILogger<TelegramAlertService> _logger;
    private readonly string? _sendMessageUrl;
    private readonly string? _chatId;

    public TelegramAlertService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<TelegramAlertService> logger)
    {
        _http = httpFactory.CreateClient();
        _logger = logger;
        var token = config["Telegram:BotToken"];
        _sendMessageUrl = string.IsNullOrWhiteSpace(token)
            ? null
            : $"https://api.telegram.org/bot{token}/sendMessage";
        _chatId = config["Telegram:ChatId"];
    }

    public Task SendCriticalAsync(string message)
        => SendAsync($"CRITICAL\n{message}");

    public Task SendTradeAlertAsync(string message)
        => SendAsync($"TRADE\n{message}");

    private async Task SendAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(_sendMessageUrl) || string.IsNullOrWhiteSpace(_chatId))
            return;

        try
        {
            var body = new { chat_id = _chatId, text, parse_mode = "HTML" };
            var response = await _http.PostAsJsonAsync(_sendMessageUrl, body);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TelegramAlertService: failed to send message");
        }
    }
}
