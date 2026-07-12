using Trading.Domain;
using Trading.Application.Interfaces;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.SignalEngine.Services;

public class HistoricalCandleService
{
    private readonly IHistoricalDataService _historical;
    private readonly IRepository<Candle> _candleRepo;

    public HistoricalCandleService(
        IHistoricalDataService historical,
        IRepository<Candle> candleRepo)
    {
        _historical = historical;
        _candleRepo = candleRepo;
    }

    public async Task<List<Candle>> DownloadAsync(
        string symbol,
        string exchange = GrowwExchange.Nse,
        string token = "NIFTY",
        string interval = GrowwInterval.FiveMinute,
        int lookbackDays = 30,
        string segment = GrowwSegment.Cash)
    {
        var from = DateTime.Today.AddDays(-lookbackDays);
        var to = DateTime.Now;

        var candles = await _historical.GetCandlesAsync(exchange, token, interval, from, to, segment);

        // Skip candles already in DB to avoid duplicate rows corrupting EMA.
        var existing = (await _candleRepo.FindAsync(
                c => c.Symbol == symbol && c.CandleTime >= from))
            .Select(c => c.CandleTime)
            .ToHashSet();

        foreach (var candle in candles)
        {
            candle.Symbol = symbol;
            if (!existing.Contains(candle.CandleTime))
                await _candleRepo.AddAsync(candle);
        }

        return candles;
    }

    public async Task<List<Candle>> GetFromDbAsync(string symbol, int days = 30)
    {
        var cutoff = IstClock.Now.AddDays(-days);
        return (await _candleRepo.FindAsync(
                c => c.Symbol == symbol && c.CandleTime >= cutoff))
            .OrderBy(c => c.CandleTime)
            .ToList();
    }
}
