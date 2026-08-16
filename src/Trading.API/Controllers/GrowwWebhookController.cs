using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

// Receives order update webhooks from Groww (if/when Groww adds postback support).
[ApiController]
[Route("api/webhook")]
[AllowAnonymous]
public class GrowwWebhookController : ControllerBase
{
    private readonly IRepository<Order> _orders;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifier;
    private readonly ILogger<GrowwWebhookController> _logger;
    private readonly IConfiguration _configuration;

    public GrowwWebhookController(
        IRepository<Order> orders,
        IUnitOfWork uow,
        INotificationService notifier,
        ILogger<GrowwWebhookController> logger,
        IConfiguration configuration)
    {
        _orders = orders;
        _uow = uow;
        _notifier = notifier;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("order")]
    public async Task<IActionResult> ReceiveOrderUpdate()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return Ok();

        if (!IsValidSignature(body))
            return Unauthorized();

        GrowwOrderWebhookDto? dto;
        try
        {
            dto = GrowwJsonHelper.Deserialize<GrowwOrderWebhookDto>(body);
        }
        catch (Exception)
        {
            return Ok();
        }

        if (dto is null || string.IsNullOrEmpty(dto.GrowwOrderId))
            return Ok();

        var order = await _orders.FirstOrDefaultAsync(o => o.BrokerOrderId == dto.GrowwOrderId);

        if (order is null)
        {
            order = new Order { BrokerOrderId = dto.GrowwOrderId, OrderedAt = IstClock.Now };
            order.Symbol = dto.TradingSymbol;
            order.Exchange = dto.Exchange;
            order.Side = dto.TransactionType;
            order.OrderType = dto.OrderType;
            order.ProductType = dto.Product;
            order.Status = dto.OrderStatus ?? "";
            order.StatusMessage = dto.Remark ?? "";
            order.Price = dto.Price ?? 0m;
            order.TriggerPrice = dto.TriggerPrice ?? 0m;
            order.Quantity = dto.Quantity ?? 0;
            order.FilledQty = dto.FilledQuantity ?? 0;
            order.AvgPrice = dto.AverageFillPrice ?? 0m;
            order.UpdatedAt = IstClock.Now;
            order.RawPostback = body;
            await _orders.StageAsync(order);
            await _uow.SaveChangesAsync();
        }
        else
        {
            order.Symbol = dto.TradingSymbol;
            order.Exchange = dto.Exchange;
            order.Side = dto.TransactionType;
            order.OrderType = dto.OrderType;
            order.ProductType = dto.Product;
            order.Status = dto.OrderStatus ?? "";
            order.StatusMessage = dto.Remark ?? "";
            order.Price = dto.Price ?? 0m;
            order.TriggerPrice = dto.TriggerPrice ?? 0m;
            order.Quantity = dto.Quantity ?? 0;
            order.FilledQty = dto.FilledQuantity ?? 0;
            order.AvgPrice = dto.AverageFillPrice ?? 0m;
            order.UpdatedAt = IstClock.Now;
            order.RawPostback = body;
            await _uow.SaveChangesAsync();
        }

        await _notifier.SendSignalAsync(new Signal
        {
            Symbol = dto.TradingSymbol,
            SignalType = $"ORDER_{dto.OrderStatus}",
            CreatedOn = IstClock.Now
        });

        return Ok();
    }

    private bool IsValidSignature(string body)
    {
        var secret = _configuration["Groww:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue("X-Groww-Signature", out var provided))
            return false;

        var expectedBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(body));
        var expected = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided.ToString().Trim().ToLowerInvariant()));
    }
}

public class GrowwOrderWebhookDto
{
    public string GrowwOrderId { get; set; } = "";
    public string TradingSymbol { get; set; } = "";
    public string Exchange { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public string OrderType { get; set; } = "";
    public string Product { get; set; } = "";
    public string? OrderStatus { get; set; }
    public string? Remark { get; set; }
    public decimal? Price { get; set; }
    public decimal? TriggerPrice { get; set; }
    public int? Quantity { get; set; }
    public int? FilledQuantity { get; set; }
    public decimal? AverageFillPrice { get; set; }
    public string? OrderReferenceId { get; set; }
}
