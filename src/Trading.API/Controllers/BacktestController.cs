using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Application.Interfaces;
using Trading.SignalEngine.Services;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BacktestController
    : ControllerBase
{
    private readonly
    IHistoricalDataService _historicalService;

    private readonly
    BacktestService _backtestService;

    public BacktestController(
        IHistoricalDataService historicalService,
        BacktestService backtestService)
    {
        _historicalService =
            historicalService;

        _backtestService =
            backtestService;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        try
        {
            // Angel One limits historical data to ~7 days per request for 5-min candles.
            // Fetch in weekly chunks and merge.
            var allCandles = new List<Trading.Domain.Entities.Candle>();
            var end = IstClock.Now;
            var start = IstClock.Now.Date.AddDays(-2);

            for (var chunkStart = start; chunkStart < end; chunkStart = chunkStart.AddDays(7))
            {
                // Skip if entire chunk falls on weekend
                var chunkEnd = chunkStart.AddDays(7) > end ? end : chunkStart.AddDays(7);
                var from = chunkStart.DayOfWeek == DayOfWeek.Saturday ? chunkStart.AddDays(2)
                         : chunkStart.DayOfWeek == DayOfWeek.Sunday ? chunkStart.AddDays(1)
                         : chunkStart;
                if (from >= chunkEnd) continue;

                var chunk = await _historicalService.GetCandlesAsync(
                    GrowwExchange.Nse, "NIFTY", GrowwInterval.FiveMinute, from, chunkEnd,
                    segment: GrowwSegment.Fno);
                allCandles.AddRange(chunk);
                await Task.Delay(600);
            }

            var result = _backtestService.Run(allCandles);
            Console.WriteLine($"[Backtest] Candles={allCandles.Count} Trades={result.TotalTrades} NetProfit={result.NetProfit}");
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Backtest] ERROR: {ex.Message}");
            return StatusCode(500, new { message = ex.Message });
        }
    }
}