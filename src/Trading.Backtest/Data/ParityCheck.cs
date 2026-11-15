using Microsoft.EntityFrameworkCore;
using Trading.Infrastructure.Data;

namespace Trading.Backtest.Data;

public sealed class ParitySnapshot
{
    public string Symbol { get; set; } = "";
    public DateTime SnapshotTime { get; set; }
    public decimal Ltp { get; set; }
    public decimal Ema20 { get; set; }
    public decimal Ema50 { get; set; }
    public decimal Rsi { get; set; }
    public decimal? Rsi5m { get; set; }
    public decimal AtrPct { get; set; }
    public decimal PriceChangePct { get; set; }
    public decimal Ema20_5m { get; set; }
    public decimal Ema50_5m { get; set; }
}

/// <summary>
/// Compares the rebuilt indicators with the values the live engine stored for the same minute (snapshots taken in the last
/// ten seconds of a minute see exactly the bars closed at the next minute boundary, which is what a decision point sees).
/// A large difference means the rebuild disagrees with production for that field, so results that depend on it are suspect.
/// </summary>
public static class ParityCheck
{
    public static async Task<List<ParitySnapshot>> LoadAsync(DbContextOptions<TradingDbContext> options, DateTime fromIst, CancellationToken ct = default)
    {
        await using var db = new TradingDbContext(options);
        return await db.Database.SqlQuery<ParitySnapshot>($@"
            WITH x AS (
                SELECT Symbol, SnapshotTime, Ltp, Ema20, Ema50, Rsi, Rsi5m, AtrPct, PriceChangePct, Ema20_5m, Ema50_5m,
                       ROW_NUMBER() OVER (PARTITION BY Symbol, DATEADD(minute, DATEDIFF(minute, 0, SnapshotTime), 0)
                                          ORDER BY SnapshotTime DESC) AS rn
                FROM IndicatorSnapshots WITH (NOLOCK)
                WHERE SnapshotTime >= {fromIst} AND DATEPART(second, SnapshotTime) >= 50)
            SELECT Symbol, SnapshotTime, Ltp, Ema20, Ema50, Rsi, Rsi5m, AtrPct, PriceChangePct, Ema20_5m, Ema50_5m FROM x WHERE rn = 1")
            .ToListAsync(ct);
    }

    public static IEnumerable<string> Compare(string symbol, IReadOnlyList<MarketPoint> points, IEnumerable<ParitySnapshot> snapshots)
    {
        var byMinute = points.ToDictionary(p => p.TimeIst);
        var diffs = new Dictionary<string, List<double>>
        {
            ["Ltp"] = [], ["Ema20"] = [], ["Ema50"] = [], ["Rsi1m"] = [], ["Rsi5m"] = [], ["AtrPct"] = [], ["DayChangePct"] = [], ["Ema20_5m"] = [], ["Ema50_5m"] = [],
        };

        foreach (var s in snapshots.Where(s => s.Symbol == symbol))
        {
            var minute = new DateTime(s.SnapshotTime.Year, s.SnapshotTime.Month, s.SnapshotTime.Day, s.SnapshotTime.Hour, s.SnapshotTime.Minute, 0);
            if (!byMinute.TryGetValue(minute, out var p)) continue;
            diffs["Ltp"].Add(Math.Abs(p.Ltp - (double)s.Ltp));
            diffs["Ema20"].Add(Math.Abs(p.Ema20 - (double)s.Ema20));
            diffs["Ema50"].Add(Math.Abs(p.Ema50 - (double)s.Ema50));
            diffs["Rsi1m"].Add(Math.Abs(p.Rsi1m - (double)s.Rsi));
            if (p.Rsi5m is { } r && s.Rsi5m is { } sr) diffs["Rsi5m"].Add(Math.Abs(r - (double)sr));
            diffs["AtrPct"].Add(Math.Abs(p.AtrPct - (double)s.AtrPct));
            diffs["DayChangePct"].Add(Math.Abs(p.DayChangePct - (double)s.PriceChangePct));
            if (s.Ema20_5m > 0) diffs["Ema20_5m"].Add(Math.Abs(p.Ema20_5m - (double)s.Ema20_5m));
            if (s.Ema50_5m > 0) diffs["Ema50_5m"].Add(Math.Abs(p.Ema50_5m - (double)s.Ema50_5m));
        }

        foreach (var (field, list) in diffs)
        {
            if (list.Count == 0) { yield return $"  {symbol,-10} {field,-13} n=0"; continue; }
            list.Sort();
            var median = list[list.Count / 2];
            var p95 = list[(int)(list.Count * 0.95)];
            var within = 100.0 * list.Count(d => d < 0.05) / list.Count;
            yield return $"  {symbol,-10} {field,-13} n={list.Count,6}  median |diff| {median,9:F4}  p95 {p95,9:F4}  within 0.05: {within,5:F1}%";
        }
    }
}
