using Trading.Domain.Entities;

namespace Trading.Infrastructure.Repositories;

/// <summary>
/// Latest-price reads on MarketTicks. Always filter by Symbol: the only index is
/// IX_MarketTick_Symbol_TickTime (Symbol leads), so a TickTime-only filter scans the whole table
/// under shared page locks and deadlocks with MarketFeedWorker's per-second tick insert
/// (22 ExitWorker deadlocks on 2026-10-07).
/// </summary>
public static class MarketTickQueries
{
    /// <summary>
    /// Latest LastPrice per requested symbol among ticks at or after <paramref name="cutoff"/>: one
    /// TOP(1) index seek per distinct symbol. Symbols with no tick in the window are left out.
    /// </summary>
    public static async Task<Dictionary<string, decimal>> LatestPricesAsync(
        this IRepository<MarketTick> tickRepo,
        IEnumerable<string?> symbols,
        DateTime cutoff)
    {
        var ltpBySymbol = new Dictionary<string, decimal>();

        foreach (var symbol in symbols.Where(s => !string.IsNullOrEmpty(s)).Distinct())
        {
            var latest = (await tickRepo.FindAsync(
                    t => t.Symbol == symbol && t.TickTime >= cutoff,
                    q => q.OrderByDescending(t => t.TickTime).Take(1)))
                .FirstOrDefault();

            if (latest is not null)
                ltpBySymbol[symbol!] = latest.LastPrice;
        }

        return ltpBySymbol;
    }

    /// <summary>
    /// Median of the last <paramref name="count"/> LastPrice values per symbol at or after
    /// <paramref name="cutoff"/> (one TOP(N) seek per symbol). With fewer than <paramref name="count"/>
    /// ticks the latest tick is used. Rejects a single bad print: on 2026-10-26 a lone BANKNIFTY tick equal
    /// to the previous close armed index-T1 protection and closed signal 10287 nine seconds after entry.
    /// </summary>
    public static async Task<Dictionary<string, decimal>> MedianRecentPricesAsync(
        this IRepository<MarketTick> tickRepo,
        IEnumerable<string?> symbols,
        DateTime cutoff,
        int count = 3)
    {
        var ltpBySymbol = new Dictionary<string, decimal>();

        foreach (var symbol in symbols.Where(s => !string.IsNullOrEmpty(s)).Distinct())
        {
            var recent = await tickRepo.FindAsync(
                t => t.Symbol == symbol && t.TickTime >= cutoff,
                q => q.OrderByDescending(t => t.TickTime).Take(count));

            if (recent.Count == 0) continue;

            ltpBySymbol[symbol!] = recent.Count < count
                ? recent[0].LastPrice
                : recent.Select(t => t.LastPrice).OrderBy(p => p).ElementAt(count / 2);
        }

        return ltpBySymbol;
    }
}
