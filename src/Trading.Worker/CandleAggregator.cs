using Trading.Domain.Entities;

namespace Trading.Worker;

public static class CandleAggregator
{
    public static DateTime FloorToBar(DateTime time, int minutes)
    {
        var totalMinutes = (long)time.TimeOfDay.TotalMinutes;
        var barMinute = (totalMinutes / minutes) * minutes;
        return time.Date.AddMinutes(barMinute);
    }

    public static List<Candle> AggregateTicksToCandles(
        string candleSymbol,
        List<MarketTick> ticks,
        int barMinutes)
    {
        if (ticks.Count == 0) return [];

        return ticks
            .GroupBy(t => FloorToBar(t.TickTime, barMinutes))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(t => t.TickTime).ToList();
                var maxVol = g.Max(t => t.Volume);
                var minVol = g.Min(t => t.Volume);
                var volume = Math.Max(0L, maxVol - minVol);

                return new Candle
                {
                    Symbol     = candleSymbol,
                    CandleTime = g.Key,
                    Open       = ordered.First().LastPrice,
                    High       = g.Max(t => t.LastPrice),
                    Low        = g.Min(t => t.LastPrice),
                    Close      = ordered.Last().LastPrice,
                    Volume     = volume
                };
            })
            .ToList();
    }

    public static List<Candle> Resample(string candleSymbol, List<Candle> candles1m, int minutes)
    {
        if (candles1m.Count == 0) return [];

        return candles1m
            .GroupBy(c => FloorToBar(c.CandleTime, minutes))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(c => c.CandleTime).ToList();
                return new Candle
                {
                    Symbol     = candleSymbol,
                    CandleTime = g.Key,
                    Open       = ordered.First().Open,
                    High       = g.Max(c => c.High),
                    Low        = g.Min(c => c.Low),
                    Close      = ordered.Last().Close,
                    Volume     = g.Sum(c => c.Volume)
                };
            })
            .ToList();
    }

    // Merges incoming candles into the existing dictionary and the toInsert list.
    // Returns (added, updated) counts.
    // Preserves High/Low extremes — never lowers an established High, never raises an established Low.
    public static (int added, int updated) MergeCandles(
        string candleSymbol,
        List<Candle> incoming,
        Dictionary<DateTime, Candle> existing,
        List<Candle> toInsert)
    {
        int added = 0, updated = 0;

        foreach (var raw in incoming)
        {
            if (existing.TryGetValue(raw.CandleTime, out var live))
            {
                bool changed = false;
                if (raw.High > live.High)      { live.High   = raw.High;   changed = true; }
                if (raw.Low  < live.Low)       { live.Low    = raw.Low;    changed = true; }
                if (raw.Close != live.Close)   { live.Close  = raw.Close;  changed = true; }
                if (raw.Volume != live.Volume) { live.Volume = raw.Volume; changed = true; }

                if (changed) updated++;
            }
            else
            {
                var candle = new Candle
                {
                    Symbol     = candleSymbol,
                    CandleTime = raw.CandleTime,
                    Open       = raw.Open,
                    High       = raw.High,
                    Low        = raw.Low,
                    Close      = raw.Close,
                    Volume     = raw.Volume
                };
                toInsert.Add(candle);
                existing[raw.CandleTime] = candle;
                added++;
            }
        }

        return (added, updated);
    }
}
