using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public class AtrService
{
    public decimal Calculate(List<Candle> candles, int period = 14)
    {
        if (candles.Count < period + 1)
            return 0;

        var trs = new List<decimal>();
        for (int i = 1; i < candles.Count; i++)
        {
            var high = candles[i].High;
            var low = candles[i].Low;
            var prev = candles[i - 1].Close;

            var tr = Math.Max(high - low,
                     Math.Max(Math.Abs(high - prev),
                              Math.Abs(low - prev)));
            trs.Add(tr);
        }

        // Wilder's smoothed ATR
        decimal atr = trs.Take(period).Average();
        for (int i = period; i < trs.Count; i++)
            atr = ((atr * (period - 1)) + trs[i]) / period;

        return atr;
    }
}
