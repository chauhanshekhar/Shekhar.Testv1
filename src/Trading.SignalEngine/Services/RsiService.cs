namespace Trading.SignalEngine.Services;

public class RsiService
{
    /// <summary>
    /// Calculates Wilder's RSI.
    /// Returns:
    /// - 50 when there are insufficient candles or no price movement.
    /// - 100 when there are only gains.
    /// - 0 when there are only losses.
    /// </summary>
    public decimal GetRsi(List<decimal> closes, int period = 14)
    {
        if (closes == null || closes.Count <= period)
            return 50m;

        var gains = new List<decimal>(closes.Count - 1);
        var losses = new List<decimal>(closes.Count - 1);

        // Calculate gains and losses
        for (int i = 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];

            gains.Add(change > 0 ? change : 0m);
            losses.Add(change < 0 ? Math.Abs(change) : 0m);
        }

        // Initial average (simple average)
        decimal avgGain = gains.Take(period).Average();
        decimal avgLoss = losses.Take(period).Average();

        // Wilder's smoothing
        for (int i = period; i < gains.Count; i++)
        {
            avgGain = ((avgGain * (period - 1)) + gains[i]) / period;
            avgLoss = ((avgLoss * (period - 1)) + losses[i]) / period;
        }

        // Flat market
        if (avgGain == 0m && avgLoss == 0m)
            return 50m;

        // Only gains
        if (avgLoss == 0m)
            return 100m;

        // Only losses
        if (avgGain == 0m)
            return 0m;

        decimal rs = avgGain / avgLoss;
        decimal rsi = 100m - (100m / (1m + rs));

        return Math.Round(rsi, 2);
    }
}