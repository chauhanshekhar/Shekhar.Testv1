using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TradeJournalController : ControllerBase
{
    private readonly IRepository<TradeJournal> _repo;
    private readonly IUnitOfWork _uow;

    public TradeJournalController(IRepository<TradeJournal> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? symbol = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var all = await _repo.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(symbol))
            all = all.Where(t => t.Symbol.Contains(symbol, StringComparison.OrdinalIgnoreCase)).ToList();

        if (from.HasValue)
            all = all.Where(t => t.TradeDate >= from.Value.Date).ToList();

        if (to.HasValue)
            all = all.Where(t => t.TradeDate < to.Value.Date.AddDays(1)).ToList();

        var total = all.Count;
        var data = all.OrderByDescending(t => t.TradeDate)
                      .Skip((page - 1) * size)
                      .Take(size);

        return Ok(new { total, page, size, data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TradeJournalRequest req)
    {
        var entry = new TradeJournal
        {
            Symbol = req.Symbol,
            TradeType = req.TradeType,
            EntryPrice = req.EntryPrice,
            ExitPrice = req.ExitPrice,
            Quantity = req.Quantity,
            ProfitLoss = (req.ExitPrice - req.EntryPrice) * req.Quantity *
                         (req.TradeType == "SELL" ? -1 : 1),
            TradeDate = IstClock.Now,
            Remarks = req.Remarks ?? ""
        };

        await _repo.AddAsync(entry);
        await _uow.SaveChangesAsync();
        return Ok(entry);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var trades = await _repo.GetAllAsync();
        return Ok(new
        {
            Total = trades.Count,
            Winners = trades.Count(t => t.ProfitLoss > 0),
            Losers = trades.Count(t => t.ProfitLoss < 0),
            NetPnL = trades.Sum(t => t.ProfitLoss),
            GrossProfit = trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss),
            GrossLoss = trades.Where(t => t.ProfitLoss < 0).Sum(t => t.ProfitLoss)
        });
    }
}

public record TradeJournalRequest(
    string Symbol,
    string TradeType,
    decimal EntryPrice,
    decimal ExitPrice,
    int Quantity,
    string? Remarks);
