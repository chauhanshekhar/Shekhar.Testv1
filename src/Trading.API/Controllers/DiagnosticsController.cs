using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Application.Interfaces;
using Trading.Infrastructure.Brokers.Groww;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/diag")]
[Authorize(Roles = "Admin,Super Admin")]
public class DiagnosticsController : ControllerBase
{
    private readonly IGrowwAuthService _auth;
    private readonly IHistoricalDataService _historical;

    public DiagnosticsController(
        IGrowwAuthService auth,
        IHistoricalDataService historical)
    {
        _auth = auth;
        _historical = historical;
    }

    // Test 1 — verify token is configured: GET /api/diag/auth
    [HttpGet("auth")]
    public IActionResult TestAuth()
    {
        var token = _auth.GetAccessToken();
        return Ok(new
        {
            tokenConfigured = !string.IsNullOrEmpty(token),
            tokenPreview = token.Length > 20 ? token[..6] + "..." + token[^4..] : "***"
        });
    }

    // Test 2 — fetch today's NIFTY 1-min candles: GET /api/diag/today?symbol=NIFTY&token=NIFTY
    [HttpGet("today")]
    public async Task<IActionResult> TodayCandles(
        [FromQuery] string symbol = "NIFTY",
        [FromQuery] string token = "NIFTY",
        [FromQuery] string exchange = GrowwExchange.Nse,
        [FromQuery] string interval = GrowwInterval.OneMinute)
    {
        var ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(IstClock.Now, ist);
        var fromIst = nowIst.Date.AddHours(9).AddMinutes(15);
        var toIst = nowIst;

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromIst, ist);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toIst, ist);

        var candles = await _historical.GetCandlesAsync(exchange, token, interval, fromUtc, toUtc);

        return Ok(new
        {
            symbol,
            exchange,
            interval,
            from = fromIst.ToString("yyyy-MM-dd HH:mm"),
            to = toIst.ToString("yyyy-MM-dd HH:mm"),
            count = candles.Count,
            candles
        });
    }
}
