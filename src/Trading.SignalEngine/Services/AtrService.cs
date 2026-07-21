using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public class AtrService
{
    public decimal Calculate(List<Candle> candles, int period = 14)
    {
        // Need at least (period + 1) candles because
        // the first True Range uses the previous candle's close.
        if (candles == null || candles.Count < period + 1)
            return 0m;

        // Pre-allocate to avoid internal list resizing.
        var trs = new List<decimal>(candles.Count - 1);

        // Calculate True Range (TR) for each candle.
        for (int i = 1; i < candles.Count; i++)
        {
            var current = candles[i];
            var previous = candles[i - 1];

            decimal tr = Math.Max(
                current.High - current.Low,
                Math.Max(
                    Math.Abs(current.High - previous.Close),
                    Math.Abs(current.Low - previous.Close)));

            trs.Add(tr);
        }

        // Initial ATR = Simple Average of first 'period' True Ranges.
        decimal atr = trs.Take(period).Average();

        // Wilder's smoothing.
        for (int i = period; i < trs.Count; i++)
        {
            atr = ((atr * (period - 1)) + trs[i]) / period;
        }

        return atr;
    }
}
