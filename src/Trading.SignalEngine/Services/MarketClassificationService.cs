
using Trading.Domain;
using Trading.Infrastructure;

namespace Trading.SignalEngine.Services;

public enum MarketState
{
    Trend,
    StrongTrend,
    Range,
    Breakout,
    Breakdown,
    Reversal,
    Scalping,
    News,
    NoTrade
}

public record MarketClassification(MarketState State, string Reason);

public class MarketClassificationService
{
    public MarketClassification Classify(
        string symbol,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal atrRaw,
        decimal rsi,
        decimal ltp,
        decimal priceChangePct,
        bool isGapUp,
        bool isGapDown,
        bool isHighImpactEvent,
        TradingSettings settings)
    {
        // 1. News protection
        if (isHighImpactEvent)
        {
            return new MarketClassification(
                MarketState.News,
                $"{symbol}: high-impact news event");
        }

        if (ema50 <= 0 || ema50_5m <= 0 || ema20_5m <= 0 ||
            atrRaw <= 0 || ltp <= 0)
        {
            return new MarketClassification(
                MarketState.NoTrade,
                $"{symbol}: invalid indicator data");
        }

        var atrMaxThreshold =
            MarketRegimeService.GetAtrMaxThreshold(symbol);

        var atrMinThreshold =
            MarketRegimeService.GetAtrThreshold(symbol);

        bool extremeVolatility = atrRaw >= atrMaxThreshold;

        // 2. EMA direction
        var emaGapPct =
            Math.Abs(ema20 - ema50) / ema50 * 100m;

        bool trendUp = ema20 > ema50;
        bool trendDown = ema20 < ema50;

        bool agrees5m =
            (trendUp && ema20_5m >= ema50_5m) ||
            (trendDown && ema20_5m <= ema50_5m);

        bool priceAboveEma = ltp > ema20;
        bool priceBelowEma = ltp < ema20;

        bool bullishMomentum =
            priceChangePct >= settings.BreakoutPriceChangeFloorPct &&
            priceAboveEma &&
            rsi > settings.RsiBullConfirm;

        bool bearishMomentum =
            priceChangePct <= -settings.BreakoutPriceChangeFloorPct &&
            priceBelowEma &&
            rsi < settings.RsiBearConfirm;

        // 3. Strong trend
        if (emaGapPct >= settings.StrongTrendEmaGapPct &&
            agrees5m &&
            ((trendUp && rsi > settings.RsiBullConfirm) ||
             (trendDown && rsi < settings.RsiBearConfirm)))
        {
            return new MarketClassification(
                MarketState.StrongTrend,
                $"{symbol}: strong {(trendUp ? "up" : "down")} trend - " +
                $"EMA gap {emaGapPct:F2}%, RSI {rsi:F0}");
        }

        // 4. Breakout / Breakdown
        // Allow early momentum before 5m EMA alignment.
        // Gap direction can also support an early directional move.
        bool bullishBreakout =
            bullishMomentum &&
            (agrees5m || trendUp || isGapUp);

        bool bearishBreakdown =
            bearishMomentum &&
            (agrees5m || trendDown || isGapDown);

        if (bullishBreakout)
        {
            return new MarketClassification(
                MarketState.Breakout,
                $"{symbol}: bullish momentum breakout - " +
                $"Change {priceChangePct:F2}%, RSI {rsi:F0}");
        }

        if (bearishBreakdown)
        {
            return new MarketClassification(
                MarketState.Breakdown,
                $"{symbol}: bearish momentum breakdown - " +
                $"Change {priceChangePct:F2}%, RSI {rsi:F0}");
        }

        // 5. Normal trend
        if (emaGapPct >= settings.TrendEmaGapPct && agrees5m)
        {
            return new MarketClassification(
                MarketState.Trend,
                $"{symbol}: {(trendUp ? "up" : "down")} trend - " +
                $"EMA gap {emaGapPct:F2}%");
        }

        // 6. Extreme volatility protection
        // Extreme ATR is not automatically tradable.
        // Only confirmed strong trends and breakouts pass above.
        if (extremeVolatility)
        {
            return new MarketClassification(
                MarketState.NoTrade,
                $"{symbol}: extreme volatility without confirmed direction " +
                $"(ATR {atrRaw:F1} >= {atrMaxThreshold:F1})");
        }

        // 7. Reversal
        bool reversalUp =
            trendUp &&
            ema20_5m < ema50_5m &&
            rsi > 50m &&
            rsi < settings.RsiBullConfirm;

        bool reversalDown =
            trendDown &&
            ema20_5m > ema50_5m &&
            rsi < 50m &&
            rsi > settings.RsiBearConfirm;

        if (reversalUp || reversalDown)
        {
            return new MarketClassification(
                MarketState.Reversal,
                $"{symbol}: potential reversal - RSI {rsi:F0}");
        }

        // 8. Scalping
        if (emaGapPct < settings.TrendEmaGapPct &&
            atrRaw >= atrMinThreshold)
        {
            return new MarketClassification(
                MarketState.Scalping,
                $"{symbol}: flat EMA with healthy ATR - " +
                $"EMA gap {emaGapPct:F2}%, ATR {atrRaw:F1}");
        }

        // 9. Range
        return new MarketClassification(
            MarketState.Range,
            $"{symbol}: no clear trend - EMA gap {emaGapPct:F2}%");
    }
}
