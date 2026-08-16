using Trading.SignalEngine.Services;

namespace Trading.SignalEngine.Strategies.Expiry;

public record MarketScoreBreakdown(
    decimal EmaTrendScore,
    decimal Ema5TrendScore,
    decimal RsiScore,
    decimal PcrScore,
    decimal AtrScore,
    decimal MarketVolumeScore,
    decimal PriceChangeScore)
{
    /// <summary>
    /// Raw sum, max 70 (7 components x 10)
    /// </summary>
    public decimal Raw =>
        EmaTrendScore +
        Ema5TrendScore +
        RsiScore +
        PcrScore +
        AtrScore +
        MarketVolumeScore +
        PriceChangeScore;

    /// <summary>
    /// Normalized Market Score (0–100)
    /// </summary>
    public decimal Normalized =>
        Raw / ExpiryMarketScoreService.RawMax * 100m;
}

public class ExpiryMarketScoreService
{
    private const decimal Max = 10m;

    public const decimal RawMax = Max * 7m;

    public MarketScoreBreakdown Score(
        string symbol,
        bool isCall,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal rsi,
        decimal pcrValue,
        decimal atrRaw,
        decimal currentVolume,
        decimal avgVolume20,
        decimal priceChangePct)
    {
        //----------------------------------------------------
        // EMA Trend
        //----------------------------------------------------

        var emaTrendScore =
            isCall
                ? (ema20 > ema50 ? Max : 0m)
                : (ema20 < ema50 ? Max : 0m);

        //----------------------------------------------------
        // 5 Minute EMA Trend
        //----------------------------------------------------

        var ema5TrendScore =
            isCall
                ? (ema20_5m > 0 && ema20_5m > ema50_5m ? Max : 0m)
                : (ema20_5m > 0 && ema20_5m < ema50_5m ? Max : 0m);

        //----------------------------------------------------
        // RSI
        //----------------------------------------------------

        decimal rsiScore =
            isCall
                ? (rsi >= 60m ? Max :
                   rsi >= 52m ? Max * 0.6m : 0m)
                : (rsi <= 40m ? Max :
                   rsi <= 48m ? Max * 0.6m : 0m);

        //----------------------------------------------------
        // PCR
        //----------------------------------------------------

        decimal pcrScore =
            isCall
                ? (pcrValue < 0.80m ? Max :
                   pcrValue < 1.00m ? Max * 0.5m : 0m)
                : (pcrValue > 1.20m ? Max :
                   pcrValue > 1.00m ? Max * 0.5m : 0m);

        //----------------------------------------------------
        // ATR
        //----------------------------------------------------

        var atrThreshold =
            MarketRegimeService.GetAtrThreshold(symbol);

        var atrMaxThreshold =
            MarketRegimeService.GetAtrMaxThreshold(symbol);

        var atrScore =
            atrRaw >= atrThreshold &&
            atrRaw < atrMaxThreshold
                ? Max
                : 0m;

        //----------------------------------------------------
        // MARKET VOLUME
        //
        // Groww/AngelOne index feed does not provide
        // reliable index volume.
        //
        // Don't penalize the market.
        //----------------------------------------------------

        decimal marketVolumeScore;

        bool volumeAvailable =
            currentVolume > 0m &&
            avgVolume20 > 0m;

        if (!volumeAvailable)
        {
            marketVolumeScore = Max;
        }
        else
        {
            var volumeRatio = currentVolume / avgVolume20;

            if (volumeRatio >= 2m)
                marketVolumeScore = Max;
            else if (volumeRatio >= 1.5m)
                marketVolumeScore = Max * 0.6m;
            else if (volumeRatio >= 1m)
                marketVolumeScore = Max * 0.3m;
            else
                marketVolumeScore = 0m;
        }

        //----------------------------------------------------
        // PRICE CHANGE
        //----------------------------------------------------

        var favorablePriceChange =
            isCall
                ? priceChangePct
                : -priceChangePct;

        decimal priceChangeScore =
            favorablePriceChange >= 0.50m ? Max :
            favorablePriceChange >= 0.20m ? Max * 0.6m :
            favorablePriceChange > 0m ? Max * 0.3m :
            0m;

        //----------------------------------------------------
        // Result
        //----------------------------------------------------

        return new MarketScoreBreakdown(
            emaTrendScore,
            ema5TrendScore,
            rsiScore,
            pcrScore,
            atrScore,
            marketVolumeScore,
            priceChangeScore);
    }
}