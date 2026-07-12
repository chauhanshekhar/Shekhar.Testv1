using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Application.Interfaces;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/broker")]
[Authorize(Roles = "Admin,Super Admin")]
public class BrokerController : ControllerBase
{
    private readonly IGrowwAuthService _auth;

    public BrokerController(IGrowwAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>
    /// Update the Groww access token. Call this each morning after generating
    /// the token from the Groww dashboard. Takes effect immediately — no restart needed.
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> UpdateToken([FromBody] UpdateTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
            return BadRequest(new { message = "AccessToken is required." });

        await _auth.UpdateAccessTokenAsync(request.AccessToken, ct);
        return Ok(new { message = "Groww access token updated successfully." });
    }

    /// <summary>
    /// Returns the first 10 and last 5 characters of the current token (masked)
    /// so you can confirm which token is active without exposing it.
    /// </summary>
    [HttpGet("token/status")]
    public IActionResult TokenStatus()
    {
        var token = _auth.GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
            return Ok(new { configured = false, masked = "" });

        var masked = token.Length > 15
            ? token[..10] + "..." + token[^5..]
            : "***";

        return Ok(new { configured = true, masked });
    }
}

public record UpdateTokenRequest(string AccessToken);
