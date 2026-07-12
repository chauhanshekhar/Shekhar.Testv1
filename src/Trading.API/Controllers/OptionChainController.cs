using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OptionChainController : ControllerBase
{
    private readonly IRepository<OptionChainData> _repo;
    private readonly IRepository<MarketTick> _tickRepo;

    public OptionChainController(IRepository<OptionChainData> repo, IRepository<MarketTick> tickRepo)
    {
        _repo = repo;
        _tickRepo = tickRepo;
    }

    // ── GET /api/OptionChain/expiries?symbol=NIFTY ──────────────────────────
    [HttpGet("expiries")]
    public async Task<IActionResult> Expiries([FromQuery] string symbol = "NIFTY")
    {
        var all = await _repo.GetAllAsync();
        var expiries = all
            .Where(o => o.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(o.Expiry))
            .Select(o => o.Expiry)
            .Distinct()
            .OrderBy(e => ParseExpiry(e))
            .ToList();

        // If no data yet, return upcoming NSE weekly expiries
        if (!expiries.Any())
            expiries = GetUpcomingExpiries(symbol);

        return Ok(expiries);
    }

    // ── GET /api/OptionChain?symbol=NIFTY&expiry=12-Jun-2026 ────────────────
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string symbol = "NIFTY",
        [FromQuery] string? expiry = null)
    {
        var all = await _repo.GetAllAsync();

        var symbolData = all
            .Where(o => o.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Filter by expiry if provided and data has expiry
        if (!string.IsNullOrWhiteSpace(expiry) && symbolData.Any(o => !string.IsNullOrWhiteSpace(o.Expiry)))
            symbolData = symbolData.Where(o => o.Expiry == expiry).ToList();

        if (!symbolData.Any())
        {
            var ticks = await _tickRepo.GetAllAsync();
            var spot = ticks
                .Where(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.TickTime)
                .FirstOrDefault()?.LastPrice;
            return Ok(GenerateSimulatedChain(symbol, expiry, spot));
        }

        // Use most recent snapshot batch
        var latestTime = symbolData.Max(o => o.CreatedOn);
        var cutoff = latestTime.AddSeconds(-5);

        var latest = symbolData
            .Where(o => o.CreatedOn >= cutoff)
            .GroupBy(o => o.StrikePrice)
            .Select(g => new
            {
                strikePrice = g.Key,
                ceLtp = g.Where(x => x.OptionType == "CE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.Ltp ?? 0,
                ceOi = g.Where(x => x.OptionType == "CE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.OpenInterest ?? 0,
                ceChangeOi = g.Where(x => x.OptionType == "CE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.ChangeInOi ?? 0,
                ceVolume = g.Where(x => x.OptionType == "CE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.Volume ?? 0,
                ceIv = g.Where(x => x.OptionType == "CE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.ImpliedVolatility ?? 0,
                peLtp = g.Where(x => x.OptionType == "PE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.Ltp ?? 0,
                peOi = g.Where(x => x.OptionType == "PE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.OpenInterest ?? 0,
                peChangeOi = g.Where(x => x.OptionType == "PE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.ChangeInOi ?? 0,
                peVolume = g.Where(x => x.OptionType == "PE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.Volume ?? 0,
                peIv = g.Where(x => x.OptionType == "PE").OrderByDescending(x => x.CreatedOn).FirstOrDefault()?.ImpliedVolatility ?? 0,
            })
            .OrderBy(x => x.strikePrice)
            .ToList();

        return Ok(latest);
    }

    // ── GET /api/OptionChain/pcr?symbol=NIFTY&expiry=12-Jun-2026 ───────────
    [HttpGet("pcr")]
    public async Task<IActionResult> Pcr(
        [FromQuery] string symbol = "NIFTY",
        [FromQuery] string? expiry = null)
    {
        // Spot price from latest MarketTick
        var ticks = await _tickRepo.GetAllAsync();
        var latestTick = ticks
            .Where(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.TickTime)
            .FirstOrDefault();

        decimal spot = latestTick?.LastPrice ?? GetDefaultSpot(symbol);
        decimal spotChange = latestTick != null ? latestTick.LastPrice - latestTick.Open : 0;
        decimal spotChangePct = spot > 0 && spotChange != 0 ? Math.Round(spotChange / (spot - spotChange) * 100, 2) : 0;

        var all = await _repo.GetAllAsync();
        var symbolData = all.Where(o => o.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(expiry) && symbolData.Any(o => !string.IsNullOrWhiteSpace(o.Expiry)))
            symbolData = symbolData.Where(o => o.Expiry == expiry).ToList();

        if (!symbolData.Any())
        {
            return Ok(new { symbol, spot, spotChange, spotChangePct, totalCeOi = 0L, totalPeOi = 0L, pcr = 0.0 });
        }

        var latestTime = symbolData.Max(o => o.CreatedOn);
        var cutoff = latestTime.AddSeconds(-5);
        var data = symbolData.Where(o => o.CreatedOn >= cutoff).ToList();

        var totalCeOi = data.Where(x => x.OptionType == "CE").Sum(x => x.OpenInterest);
        var totalPeOi = data.Where(x => x.OptionType == "PE").Sum(x => x.OpenInterest);
        var pcr = totalCeOi == 0 ? 0 : (double)totalPeOi / totalCeOi;

        return Ok(new { symbol, spot, spotChange, spotChangePct, totalCeOi, totalPeOi, pcr = Math.Round(pcr, 3) });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static DateTime ParseExpiry(string expiry)
    {
        return DateTime.TryParse(expiry, out var d) ? d : DateTime.MaxValue;
    }

    private static List<string> GetUpcomingExpiries(string symbol)
    {
        // NSE weekly: every Thursday (NIFTY/FINNIFTY), every Wednesday (BANKNIFTY)
        var today = DateTime.Today;
        var targetDay = symbol.Equals("BANKNIFTY", StringComparison.OrdinalIgnoreCase)
            ? DayOfWeek.Wednesday : DayOfWeek.Thursday;

        var expiries = new List<string>();
        var cursor = today;
        while (expiries.Count < 6)
        {
            if (cursor.DayOfWeek == targetDay)
                expiries.Add(cursor.ToString("dd-MMM-yyyy"));
            cursor = cursor.AddDays(1);
        }
        return expiries;
    }

    // Fallback spot prices — used only when no MarketTick exists yet (first startup).
    private static decimal GetDefaultSpot(string symbol) => symbol.ToUpper() switch
    {
        "BANKNIFTY"   => 55000m,
        "FINNIFTY"    => 25000m,
        "SENSEX"      => 82000m,
        "MIDCAPNIFTY" => 13000m,
        _             => 25000m   // NIFTY
    };

    /// <summary>
    /// Returns a realistic simulated option chain when no DB data exists.
    /// Allows the UI to render meaningfully during dev / before Angel One is live.
    /// </summary>
    private static List<object> GenerateSimulatedChain(string symbol, string? expiry, decimal? liveSpot = null)
    {
        var spot = liveSpot ?? GetDefaultSpot(symbol);
        var stepSize = symbol.ToUpper() is "BANKNIFTY" or "SENSEX" or "MIDCAPNIFTY" ? 100m : 50m;
        var atmStrike = Math.Round(spot / stepSize) * stepSize;
        var strikes = Enumerable.Range(-10, 21)
            .Select(i => atmStrike + i * stepSize)
            .ToList();

        var rng = new Random(42);
        // Days to expiry
        int dte = 7;
        if (!string.IsNullOrWhiteSpace(expiry) && DateTime.TryParse(expiry, out var exp))
            dte = Math.Max(1, (exp - DateTime.Today).Days);

        var result = new List<object>();
        foreach (var strike in strikes)
        {
            var dist = (double)(strike - spot);
            var ceItm = dist < 0;
            var ceLtp = ceItm
                ? (decimal)Math.Max(5, (double)(-dist) + rng.Next(5, 30)) + Math.Round((decimal)rng.NextDouble() * 5, 2)
                : (decimal)Math.Max(1, 50 - dist * 0.8 + rng.Next(1, 20)) + Math.Round((decimal)rng.NextDouble() * 5, 2);
            if (ceLtp < 0.5m) ceLtp = 0.5m;

            var peLtp = symbol.ToUpper() == "NIFTY"
                ? Math.Max(0.5m, ceLtp + (decimal)(dist * 0.9) - (spot - strike) * 0.01m)
                : Math.Max(0.5m, ceLtp - (decimal)(dist * 0.9));
            peLtp = Math.Round(Math.Abs(peLtp), 2);
            ceLtp = Math.Round(ceLtp, 2);

            long ceOi = (long)(rng.Next(100, 5000) * 1000L);
            long peOi = (long)(rng.Next(100, 5000) * 1000L);
            long ceCOi = (long)(rng.Next(-500, 1000) * 1000L);
            long peCOi = (long)(rng.Next(-500, 1000) * 1000L);

            double tSqrt = Math.Sqrt(dte / 365.0);
            decimal ceIv = tSqrt > 0 ? Math.Round((decimal)(rng.Next(12, 35) + rng.NextDouble() * 5), 2) : 0;
            decimal peIv = Math.Round(ceIv + (decimal)(rng.NextDouble() * 2 - 1), 2);

            result.Add(new
            {
                strikePrice = strike,
                ceLtp,
                ceOi,
                ceChangeOi = ceCOi,
                ceVolume = ceOi / 100,
                ceIv,
                peLtp,
                peOi,
                peChangeOi = peCOi,
                peVolume = peOi / 100,
                peIv
            });
        }
        return result;
    }
}
