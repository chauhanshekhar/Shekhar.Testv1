using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IOptionsMonitor<GrowwSettings> _options;
    private readonly IGrowwAuthService _auth;

    public SettingsController(IConfiguration config, IOptionsMonitor<GrowwSettings> options, IGrowwAuthService auth)
    {
        _config = config;
        _options = options;
        _auth = auth;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var mode = _auth.GetMode();
        var token = _auth.GetAccessToken();
        var masked = token.Length > 15 ? token[..10] + "..." + token[^5..] : (token.Length > 0 ? "***" : "");

        return Ok(new
        {
            telegramConfigured = !string.IsNullOrWhiteSpace(_config["Telegram:BotToken"]),
            growwConfigured = !string.IsNullOrWhiteSpace(token),
            growwTokenMasked = masked,
            tradingMode = mode.ToString(),
            paperTradingMode = mode == TradingMode.Paper,
            baseUrl = _options.CurrentValue.BaseUrl
        });
    }

    [HttpGet("telegram-status")]
    public IActionResult TelegramStatus()
    {
        var configured = !string.IsNullOrWhiteSpace(_config["Telegram:BotToken"]) &&
                         !string.IsNullOrWhiteSpace(_config["Telegram:ChatId"]);
        return Ok(new { configured, chatId = _config["Telegram:ChatId"] ?? "" });
    }
}
