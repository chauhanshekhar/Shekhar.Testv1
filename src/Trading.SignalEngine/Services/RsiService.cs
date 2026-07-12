namespace Trading.SignalEngine.Services;

public class RsiService
{
    /// <summary>
    /// Wilder's RSI — same smoothing as AtrService uses for ATR.
    /// Returns 50 when there are insufficient candles.
    /// </summary>
    public decimal GetRsi(List<decimal> closes, int period = 14)
    {
        if (closes.Count <= period) return 50m;

        var gains = new List<decimal>(closes.Count - 1);
        var losses = new List<decimal>(closes.Count - 1);

        for (int i = 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            gains.Add(change > 0 ? change : 0m);
            losses.Add(change < 0 ? Math.Abs(change) : 0m);
        }

        // Seed: simple average of first `period` values
        var avgGain = gains.Take(period).Average();
        var avgLoss = losses.Take(period).Average();

        // Wilder's smoothed average for remaining bars
        for (int i = period; i < gains.Count; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        if (avgLoss == 0m) return 100m;

        var rs = avgGain / avgLoss;
        return Math.Round(100m - (100m / (1m + rs)), 2);
    }
}
