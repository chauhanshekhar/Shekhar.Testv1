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
        decimal vwap,
        decimal ltp,
        decimal volumeRatio,
        decimal priceChangePct,
        bool isGapUp,
        bool isGapDown,
        bool isHighImpactEvent,
        TradingSettings settings)
    {
        if (isHighImpactEvent)
        {
            return new MarketClassification(
                MarketState.News,
                $"{symbol}: high-impact news event");
        }

        var atrMaxThreshold = MarketRegimeService.GetAtrMaxThreshold(symbol);

        if (atrRaw >= atrMaxThreshold)
        {
            return new MarketClassification(
                MarketState.NoTrade,
                $"{symbol}: extreme volatility (ATR {atrRaw:F1} >= {atrMaxThreshold:F1})");
        }

        var emaGapPct =
            ema50 == 0
                ? 0
                : Math.Abs(ema20 - ema50) / ema50 * 100m;

        bool trendUp = ema20 > ema50;
        bool trendDown = ema20 < ema50;

        bool agrees5m =
            (trendUp && ema20_5m >= ema50_5m) ||
            (trendDown && ema20_5m <= ema50_5m);

        // ----------------------------------------------------
        // Strong Trend
        // ----------------------------------------------------

        if (emaGapPct >= settings.StrongTrendEmaGapPct &&
            agrees5m &&
            ((trendUp && rsi > settings.RsiBullConfirm) ||
             (trendDown && rsi < settings.RsiBearConfirm)))
        {
            return new MarketClassification(
                MarketState.StrongTrend,
                $"{symbol}: strong {(trendUp ? "up" : "down")} trend - EMA gap {emaGapPct:F2}%, RSI {rsi:F0}");
        }

        // ----------------------------------------------------
        // Trend
        // ----------------------------------------------------

        if (emaGapPct >= settings.TrendEmaGapPct &&
            agrees5m)
        {
            return new MarketClassification(
                MarketState.Trend,
                $"{symbol}: {(trendUp ? "up" : "down")} trend - EMA gap {emaGapPct:F2}%");
        }

        // ----------------------------------------------------
        // Breakout / Breakdown
        // (No VWAP / Volume because API doesn't provide them)
        // ----------------------------------------------------

        if (Math.Abs(priceChangePct) >= settings.BreakoutPriceChangeFloorPct)
        {
            if (trendUp &&
                agrees5m &&
                rsi > settings.RsiBullConfirm &&
                priceChangePct > 0)
            {
                return new MarketClassification(
                    MarketState.Breakout,
                    $"{symbol}: bullish breakout");
            }

            if (trendDown &&
                agrees5m &&
                rsi < settings.RsiBearConfirm &&
                priceChangePct < 0)
            {
                return new MarketClassification(
                    MarketState.Breakdown,
                    $"{symbol}: bearish breakdown");
            }
        }

        // ----------------------------------------------------
        // Reversal
        // ----------------------------------------------------

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

        // ----------------------------------------------------
        // Scalping
        // ----------------------------------------------------

        if (emaGapPct < settings.TrendEmaGapPct &&
            atrRaw >= MarketRegimeService.GetAtrThreshold(symbol))
        {
            return new MarketClassification(
                MarketState.Scalping,
                $"{symbol}: flat EMA with healthy ATR");
        }

        // ----------------------------------------------------
        // Range
        // ----------------------------------------------------

        return new MarketClassification(
            MarketState.Range,
            $"{symbol}: no clear trend - EMA gap {emaGapPct:F2}%");
    }
}