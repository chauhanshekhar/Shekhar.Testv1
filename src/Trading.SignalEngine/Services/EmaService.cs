namespace Trading.SignalEngine.Services;

public class EmaService
{
    /// <summary>
    /// Calculates Exponential Moving Average (EMA).
    /// Returns 0 when there are insufficient prices.
    /// </summary>
    public decimal GetEma(
        IReadOnlyList<decimal> prices,
        int period)
    {
        if (prices == null)
            throw new ArgumentNullException(nameof(prices));

        if (period <= 0)
            throw new ArgumentOutOfRangeException(nameof(period));

        if (prices.Count < period)
            return 0m;

        // Seed EMA with SMA of first period
        decimal sum = 0m;

        for (int i = 0; i < period; i++)
        {
            sum += prices[i];
        }

        decimal ema = sum / period;

        decimal multiplier = 2m / (period + 1m);

        // Wilder/standard EMA calculation
        for (int i = period; i < prices.Count; i++)
        {
            ema = ((prices[i] - ema) * multiplier) + ema;
        }

        return Math.Round(ema, 2);
    }

    public decimal GetEma20(
        IReadOnlyList<decimal> prices)
    {
        return GetEma(prices, 20);
    }

    public decimal GetEma50(
        IReadOnlyList<decimal> prices)
    {
        return GetEma(prices, 50);
    }

    public decimal GetEma200(
        IReadOnlyList<decimal> prices)
    {
        return GetEma(prices, 200);
    }

    /// <summary>
    /// Returns true when fast EMA is above slow EMA.
    /// Useful for trend confirmation.
    /// </summary>
    public bool IsBullishTrend(
        IReadOnlyList<decimal> prices)
    {
        var ema20 = GetEma20(prices);
        var ema50 = GetEma50(prices);

        return ema20 > ema50;
    }

    /// <summary>
    /// Returns true when fast EMA is below slow EMA.
    /// Useful for trend confirmation.
    /// </summary>
    public bool IsBearishTrend(
        IReadOnlyList<decimal> prices)
    {
        var ema20 = GetEma20(prices);
        var ema50 = GetEma50(prices);

        return ema20 < ema50;
    }
}