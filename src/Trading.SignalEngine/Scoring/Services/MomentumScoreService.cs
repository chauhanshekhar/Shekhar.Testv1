using Trading.Domain.Entities;

namespace Trading.SignalEngine.Scoring.Services;

/// <summary>
/// Max 15 — reuses the same bullish/bearish 5m-candle-counting pattern already
/// added to MarketRegimeService.Classify, applied here per option direction.
/// </summary>
public class MomentumScoreService
{
    public decimal Score(
        bool isCall,
        decimal underlyingPriceChangePct,
        List<Candle> candles5m)
    {
        var last5 = candles5m.TakeLast(5).ToList();

        var confirmingCandles = isCall
            ? last5.Count(c => c.Close > c.Open)
            : last5.Count(c => c.Close < c.Open);

        var favorableChangePct = isCall ? underlyingPriceChangePct : -underlyingPriceChangePct;

        if (confirmingCandles >= 4 && favorableChangePct > 0.5m)
            return 15m;

        if (confirmingCandles >= 3 || favorableChangePct > 0.2m)
            return 8m;

        return 0m;
    }
}
