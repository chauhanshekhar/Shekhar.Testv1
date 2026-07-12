using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Application.Interfaces;
using Trading.Application.Models;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/smart-orders")]
[Authorize]
public class SmartOrdersController : ControllerBase
{
    private readonly IGrowwSmartOrderService _svc;

    public SmartOrdersController(IGrowwSmartOrderService svc)
    {
        _svc = svc;
    }

    [HttpPost("gtt")]
    public async Task<IActionResult> CreateGtt([FromBody] CreateGttRequest request)
    {
        var result = await _svc.CreateGttAsync(request);
        if (result is null) return BadRequest(new { message = "Failed to create GTT order." });
        return Ok(result);
    }

    [HttpPost("oco")]
    public async Task<IActionResult> CreateOco([FromBody] CreateOcoRequest request)
    {
        var result = await _svc.CreateOcoAsync(request);
        if (result is null) return BadRequest(new { message = "Failed to create OCO order." });
        return Ok(result);
    }

    [HttpPut("gtt/{smartOrderId}")]
    public async Task<IActionResult> ModifyGtt(string smartOrderId, [FromBody] ModifyGttRequest request)
    {
        var result = await _svc.ModifyGttAsync(smartOrderId, request);
        if (result is null) return BadRequest(new { message = "Failed to modify GTT order." });
        return Ok(result);
    }

    [HttpPut("oco/{smartOrderId}")]
    public async Task<IActionResult> ModifyOco(string smartOrderId, [FromBody] ModifyOcoRequest request)
    {
        var result = await _svc.ModifyOcoAsync(smartOrderId, request);
        if (result is null) return BadRequest(new { message = "Failed to modify OCO order." });
        return Ok(result);
    }

    [HttpPost("cancel/{segment}/{smartOrderType}/{smartOrderId}")]
    public async Task<IActionResult> Cancel(string segment, string smartOrderType, string smartOrderId)
    {
        var ok = await _svc.CancelSmartOrderAsync(segment, smartOrderType, smartOrderId);
        if (!ok) return BadRequest(new { message = "Failed to cancel smart order." });
        return Ok(new { message = "Smart order cancelled." });
    }

    [HttpGet("{segment}/{smartOrderType}/{smartOrderId}")]
    public async Task<IActionResult> GetOrder(string segment, string smartOrderType, string smartOrderId)
    {
        var result = await _svc.GetSmartOrderAsync(segment, smartOrderType, smartOrderId);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? segment = null,
        [FromQuery] string smartOrderType = "GTT",
        [FromQuery] string status = "ACTIVE",
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? startDateTime = null,
        [FromQuery] string? endDateTime = null)
    {
        var orders = await _svc.ListSmartOrdersAsync(
            segment, smartOrderType, status, page, pageSize, startDateTime, endDateTime);
        return Ok(orders);
    }
}
