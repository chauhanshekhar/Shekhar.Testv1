using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/paper-trading")]
[Authorize]
public class PaperTradingController : ControllerBase
{
    private readonly IRepository<Signal> _signals;
    private readonly IRepository<OptionChainData> _chainRepo;
    private readonly IRepository<MarketTick> _tickRepo;
    private readonly PaperTradingService _paper;

    public PaperTradingController(
        IRepository<Signal> signals,
        IRepository<OptionChainData> chainRepo,
        IRepository<MarketTick> tickRepo,
        PaperTradingService paper)
    {
        _signals = signals;
        _chainRepo = chainRepo;
        _tickRepo = tickRepo;
        _paper = paper;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var trades = await _paper.GetAllTradesAsync();
        var ltpMap = await BuildLtpMapAsync();
        var snapshots = await _paper.GetLiveSnapshotsAsync(ltpMap);
        var stats = _paper.GetStats(trades);

        return Ok(new
        {
            Trades = trades.OrderByDescending(t => t.CreatedOn),
            OpenPositions = snapshots,           // enriched with live LTP + unrealised P&L
            Stats = stats,
        });
    }

    [HttpGet("positions")]
    public async Task<IActionResult> LivePositions()
    {
        var ltpMap = await BuildLtpMapAsync();
        var snapshots = await _paper.GetLiveSnapshotsAsync(ltpMap);
        var totalPnL = snapshots.Sum(s => s.UnrealisedPnL);
        return Ok(new { positions = snapshots, totalUnrealisedPnL = totalPnL });
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] PaperOrderRequest req)
    {
        var signal = new Signal
        {
            Symbol = req.Symbol,
            OptionSymbol = req.OptionSymbol ?? req.Symbol,
            SignalType = req.Side,
            EntryPrice = req.EntryPrice,
            OptionLtp = req.EntryPrice,
            StopLoss = req.StopLoss,
            OptionSL = req.StopLoss,
            Target1 = req.Target1,
            OptionT1 = req.Target1,
            Target2 = req.Target2,
            OptionT2 = req.Target2,
        };

        var trade = await _paper.OpenAsync(signal, req.Quantity);
        return Ok(trade);
    }

    [HttpPost("close/{id}")]
    public async Task<IActionResult> Close(long id, [FromBody] CloseRequest req)
    {
        var trade = await _paper.CloseAsync(id, req.ExitPrice, req.Reason ?? "MANUAL", req.Leg2ExitPrice ?? 0m);
        if (trade is null)
            return NotFound(new { message = "Trade not found or already closed." });
        return Ok(trade);
    }

    // Auto-open paper trades from latest untraded signals (last 1 hour).
    [HttpPost("auto-trade")]
    public async Task<IActionResult> AutoTrade()
    {
        var since = IstClock.Now.AddHours(-1);
        var signals = (await _signals.GetAllAsync())
            .Where(s => s.CreatedOn >= since)
            .OrderByDescending(s => s.CreatedOn)
            .ToList();

        var openTrades = await _paper.GetOpenTradesAsync();
        var openSymbols = openTrades.Select(t => t.OptionSymbol).ToHashSet();

        var opened = new List<PaperTrade>();
        foreach (var signal in signals)
        {
            var sym = !string.IsNullOrEmpty(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol;
            if (openSymbols.Contains(sym)) continue;

            var trade = await _paper.OpenAsync(signal);
            openSymbols.Add(sym);
            opened.Add(trade);
        }

        return Ok(new { message = $"{opened.Count} trade(s) opened from signals", trades = opened });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var trades = await _paper.GetAllTradesAsync();
        return Ok(_paper.GetStats(trades));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Build optionSymbol → LTP map using the latest option chain snapshot.
    // Falls back to MarketTick if no chain data exists for a symbol.
    private async Task<IReadOnlyDictionary<string, decimal>> BuildLtpMapAsync()
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        // Latest option chain data (last 2 hours)
        var cutoff = IstClock.Now.AddHours(-2);
        var chain = await _chainRepo.FindAsync(o => o.CreatedOn >= cutoff);

        // Group by underlying symbol, take most-recent snapshot
        foreach (var grp in chain.GroupBy(o => o.Symbol))
        {
            var latestSnap = grp.OrderByDescending(o => o.CreatedOn).First().SnapshotId;
            foreach (var row in grp.Where(o => o.SnapshotId == latestSnap))
            {
                // Key is the option contract name built from chain fields
                var key = $"{row.Symbol}{row.Expiry}{row.StrikePrice}{row.OptionType}";
                map[key] = row.Ltp;
            }
        }

        // Also pull direct ticks as fallback (last 60 seconds)
        var tickCutoff = IstClock.Now.AddSeconds(-60);
        var ticks = await _tickRepo.FindAsync(t => t.TickTime >= tickCutoff);
        foreach (var grp in ticks.GroupBy(t => t.Symbol))
        {
            var latest = grp.OrderByDescending(t => t.TickTime).First();
            map.TryAdd(grp.Key, latest.LastPrice);
        }

        return map;
    }
}

public record PaperOrderRequest(
    string Symbol,
    string? OptionSymbol,
    string Side,
    decimal EntryPrice,
    decimal StopLoss = 0,
    decimal Target1 = 0,
    decimal Target2 = 0,
    int Quantity = 1);

public record CloseRequest(decimal ExitPrice, string? Reason = "MANUAL", decimal? Leg2ExitPrice = null);
