using Trading.Backtest.Data;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;

namespace Trading.Backtest.Entry;

/// <summary>
/// Runs the REAL MarketRegimeService and MarketClassificationService on the rebuilt inputs, once per decision minute (they do not depend on
/// any configuration), so the strategy-selector rules can be replayed. The regime reads the rolling 200-bar 1m window, exactly as production.
/// Gap flags are not passed: neither service uses them in a decision.
/// </summary>
public static class RegimeEnricher
{
    public static List<MarketPoint> Attach(string symbol, IReadOnlyList<MarketPoint> points, Bar[] bars1m, TradingSettings trading)
    {
        var regimeService = new MarketRegimeService(new AtrService());
        var classifier = new MarketClassificationService();
        var result = new List<MarketPoint>(points.Count);

        // 1m EMA20>EMA50 run tracking (points are consecutive decision minutes; a gap of more than 2 minutes starts a new session).
        DateTime? prevTime = null;
        var prevUp = false;
        int? barsUp = null;

        foreach (var p in points)
        {
            var up = p.Ema20 > p.Ema50;
            var contiguous = prevTime is { } pt && p.TimeUtc - pt <= TimeSpan.FromMinutes(2);
            if (!up) barsUp = null;
            else if (contiguous && prevUp) barsUp = barsUp is { } n ? n + 1 : null;
            else if (contiguous) barsUp = 0;
            else barsUp = null;
            prevTime = p.TimeUtc;
            prevUp = up;

            var from = Math.Max(0, p.NextBarIndex - MarketSeriesBuilderWindow);
            var candles = new List<Candle>(p.NextBarIndex - from);
            for (var i = from; i < p.NextBarIndex; i++)
            {
                var b = bars1m[i];
                candles.Add(new Candle { Symbol = symbol, CandleTime = b.OpenUtc, Open = (decimal)b.Open, High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close });
            }

            var regime = regimeService.Classify(symbol, candles, (decimal)p.Ema20, (decimal)p.Ema50);
            var state = classifier.Classify(
                symbol, (decimal)p.Ema20, (decimal)p.Ema50, (decimal)p.Ema20_5m, (decimal)p.Ema50_5m,
                (decimal)p.Atr, (decimal)p.Rsi1m, (decimal)p.Ltp, (decimal)p.DayChangePct,
                isGapUp: false, isGapDown: false, isHighImpactEvent: false, trading);

            result.Add(p with { RegimeTrend = regime.Trend.ToString(), RegimeVolatility = regime.Volatility.ToString(), ReplayedMarketState = state.State.ToString(), BarsSinceEma1mUp = barsUp });
        }
        return result;
    }

    private const int MarketSeriesBuilderWindow = Indicators.MarketSeriesBuilder.Window1m;
}
