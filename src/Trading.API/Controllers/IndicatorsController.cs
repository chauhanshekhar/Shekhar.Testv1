using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IndicatorsController : ControllerBase
{
    private readonly IRepository<SymbolIndicator> _repo;

    public IndicatorsController(IRepository<SymbolIndicator> repo)
    {
        _repo = repo;
    }

    // GET /api/Indicators
    // Returns every indicator row (table is tiny — max ~6 rows)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var all = await _repo.GetAllAsync();
        return Ok(all.Select(i => new
        {
            i.Symbol,
            i.IndicatorName,
            i.Value,
            i.UpdatedOn
        }));
    }

    // GET /api/Indicators/NIFTY
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetBySymbol(string symbol)
    {
        var rows = await _repo.FindAsync(
            i => i.Symbol == symbol.ToUpperInvariant());
        return Ok(rows.Select(i => new
        {
            i.Symbol,
            i.IndicatorName,
            i.Value,
            i.UpdatedOn
        }));
    }

    // GET /api/Indicators/summary
    // Grouped view: { symbol, ema20_1m, ema50_1m, updatedOn }
    // Dashboard calls this once to get all live indicators.
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var all = await _repo.GetAllAsync();

        var grouped = all
            .GroupBy(i => i.Symbol)
            .Select(g => new
            {
                Symbol = g.Key,
                // Names now include timeframe suffix — consistent with worker output
                Ema20 = g.FirstOrDefault(i => i.IndicatorName == "EMA20_1M")?.Value ?? 0,
                Ema50 = g.FirstOrDefault(i => i.IndicatorName == "EMA50_1M")?.Value ?? 0,
                UpdatedOn = g.Max(i => i.UpdatedOn)
            })
            .OrderBy(x => x.Symbol);

        return Ok(grouped);
    }
}
