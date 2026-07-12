using Microsoft.AspNetCore.Mvc;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SignalController : ControllerBase
{
    private readonly IRepository<Signal> _repository;
    private readonly IRepository<TradeJournal> _journal;

    public SignalController(
        IRepository<Signal> repository,
        IRepository<TradeJournal> journal)
    {
        _repository = repository;
        _journal = journal;
    }

    /// <summary>GET /api/signal?days=7&page=1&size=50&status=ORDER_PLACED</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int days = 7,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? status = null)
    {
        if (size > 200) size = 200;
        if (days > 90) days = 90;

        var cutoff = IstClock.Now.Date.AddDays(-days);
        var signals = await _repository.FindAsync(s => s.CreatedOn >= cutoff);

        if (!string.IsNullOrWhiteSpace(status))
            signals = signals.Where(s => s.Status == status).ToList();

        var closedSignalIds = (await _journal.FindAsync(
                j => j.SignalId.HasValue && !j.IsPartial && j.TradeDate >= cutoff))
            .Select(j => j.SignalId!.Value)
            .ToHashSet();

        var ordered = signals.OrderByDescending(s => s.CreatedOn).ToList();
        var paged   = ordered.Skip((page - 1) * size).Take(size);

        return Ok(new
        {
            Total = ordered.Count,
            Page  = page,
            Size  = size,
            Data  = paged.Select(s => new
            {
                s.Id,
                s.Symbol,
                s.SignalType,
                s.EntryPrice,
                s.StopLoss,
                s.Target1,
                s.Target2,
                s.Confidence,
                s.CreatedOn,
                s.OptionType,
                s.StrikePrice,
                s.OptionSymbol,
                s.OptionLtp,
                s.OptionSL,
                s.OptionT1,
                s.OptionT2,
                s.Exchange,
                s.Quantity,
                s.StrategyType,
                s.Leg2OptionType,
                s.Leg2StrikePrice,
                s.Leg2Symbol,
                s.Leg2Ltp,
                s.Leg2OrderId,
                s.BrokerOrderId,
                s.RemainingQty,
                s.PartialPnL,
                s.Status,
                s.ExitTime,
                s.ExitReason,
                IsActive = !closedSignalIds.Contains(s.Id),
            })
        });
    }
}
