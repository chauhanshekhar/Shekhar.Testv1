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

/// <summary>
/// Classifies the MARKET (not a signal) into a MarketState using EMA/ATR/RSI/VWAP/volume/price-change/gap
/// inputs already produced by MarketSnapshotBuilder + MarketRegimeService. Pure function over primitives —
/// no option chain, no DB reads, no signal generation. StrategySelectorService drives strategy choice off the
/// returned MarketState.
///
/// Expiry is deliberately NOT a MarketState here — it's an orthogonal attribute of the market (see
/// StrategyContext.IsExpiryDay), not a value that overrides classification. The market gets classified the
/// same way regardless of whether today is expiry; the selector layers the expiry-day time-of-day dispatch
/// on top of whatever the real classification came back as, so a 10:30 AM expiry-day trend and a 1:00 PM
/// expiry-day trend both still show up as "Trend" (with IsExpiryDay=true) instead of both being flattened
/// into an uninformative single "Expiry" bucket.
/// </summary>
public class MarketClassificationService
{
    private static readonly TimeSpan GapTrapWindow = new(9, 45, 0);

    public MarketClassification Classify(
        string symbol,
        decimal ema20, decimal ema50,
        decimal ema20_5m, decimal ema50_5m,
        decimal atrRaw,
        decimal rsi,
        decimal vwap, decimal ltp,
        decimal volumeRatio,
        decimal priceChangePct,
        bool isGapUp, bool isGapDown,
        bool isHighImpactEvent,
        TradingSettings settings)
    {
        if (isHighImpactEvent)
            return new MarketClassification(MarketState.News, $"{symbol}: high-impact news event");

        if ((isGapUp || isGapDown) && IstClock.Now.TimeOfDay < GapTrapWindow)
            return new MarketClassification(MarketState.NoTrade,
                $"{symbol}: gap {(isGapUp ? "up" : "down")} trap window (before 09:45)");

        var atrMaxThreshold = MarketRegimeService.GetAtrMaxThreshold(symbol);
        if (atrRaw >= atrMaxThreshold)
            return new MarketClassification(MarketState.NoTrade,
                $"{symbol}: extreme volatility (ATR {atrRaw:F1} >= {atrMaxThreshold:F1})");

        var emaGapPct = ema50 != 0 ? Math.Abs(ema20 - ema50) / ema50 * 100m : 0m;
        var trendUp = ema20 > ema50;
        var trendDown = ema20 < ema50;
        var agrees5m = (trendUp && ema20_5m >= ema50_5m) || (trendDown && ema20_5m <= ema50_5m);

        if (emaGapPct >= settings.StrongTrendEmaGapPct && agrees5m &&
            ((trendUp && rsi > settings.RsiBullConfirm) || (trendDown && rsi < settings.RsiBearConfirm)))
        {
            return new MarketClassification(MarketState.StrongTrend,
                $"{symbol}: strong {(trendUp ? "up" : "down")}trend — EMA gap {emaGapPct:F2}%, RSI {rsi:F0}, 5m confirms");
        }

        if (emaGapPct >= settings.TrendEmaGapPct && agrees5m)
        {
            return new MarketClassification(MarketState.Trend,
                $"{symbol}: {(trendUp ? "up" : "down")}trend — EMA gap {emaGapPct:F2}%, 5m confirms");
        }

        if (vwap > 0 && volumeRatio >= settings.BreakoutVolumeRatio && Math.Abs(priceChangePct) >= settings.BreakoutPriceChangeFloorPct)
        {
            if (ltp > vwap && priceChangePct > 0)
                return new MarketClassification(MarketState.Breakout,
                    $"{symbol}: breakout above VWAP {vwap:F1}, volume {volumeRatio:F1}x, price {priceChangePct:F2}%");
            if (ltp < vwap && priceChangePct < 0)
                return new MarketClassification(MarketState.Breakdown,
                    $"{symbol}: breakdown below VWAP {vwap:F1}, volume {volumeRatio:F1}x, price {priceChangePct:F2}%");
        }

        // 1m trend disagrees with the 5m trend while RSI is crossing back through the mid-line —
        // a potential turn, not yet a confirmed new trend.
        var reversalUp = trendUp && ema20_5m < ema50_5m && rsi > 50m && rsi < settings.RsiBullConfirm;
        var reversalDown = trendDown && ema20_5m > ema50_5m && rsi < 50m && rsi > settings.RsiBearConfirm;
        if (reversalUp || reversalDown)
        {
            return new MarketClassification(MarketState.Reversal,
                $"{symbol}: potential reversal — 1m/5m trend disagreement, RSI {rsi:F0} crossing mid-line");
        }

        if (emaGapPct < settings.TrendEmaGapPct && volumeRatio >= settings.BreakoutVolumeRatio)
        {
            return new MarketClassification(MarketState.Scalping,
                $"{symbol}: flat EMA, elevated volume {volumeRatio:F1}x — scalping conditions");
        }

        return new MarketClassification(MarketState.Range,
            $"{symbol}: no clear trend/breakout — EMA gap {emaGapPct:F2}%, volume {volumeRatio:F1}x");
    }
}
