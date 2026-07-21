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
    /// <summary>Raw sum, max 70 (7 components x ExpiryMarketScoreService.Max).</summary>
    public decimal Raw =>
        EmaTrendScore + Ema5TrendScore + RsiScore + PcrScore + AtrScore + MarketVolumeScore + PriceChangeScore;

    /// <summary>Rescaled to 0-100 — used both as the pre-option-scoring gate (ExpirySignalEngine)
    /// and as the MarketScore side of ExpiryStrikeScore.TotalScore's weighted confidence blend.</summary>
    public decimal Normalized => Raw / ExpiryMarketScoreService.RawMax * 100m;
}

/// <summary>
/// Index-level "Market Score" for the Expiry engine — max 10 per component, 70 total per side.
/// Computed once per side per cycle (same inputs for every strike of that side), mirroring how
/// ExpiryScoreService already computes OiShiftScore/PremiumMomentumScore once per side.
/// Atr/MarketVolume are direction-agnostic (reward conditions favorable to trading either side);
/// the rest are direction-aware (CE vs PE).
/// </summary>
public class ExpiryMarketScoreService
{
    private const decimal Max = 10m;

    /// <summary>7 components x Max — the raw ceiling MarketScoreBreakdown.Normalized rescales against.</summary>
    public const decimal RawMax = Max * 7m;

    public MarketScoreBreakdown Score(
        string symbol,
        bool isCall,
        decimal ema20, decimal ema50,
        decimal ema20_5m, decimal ema50_5m,
        decimal rsi,
        decimal pcrValue,
        decimal atrRaw,
        decimal currentVolume, decimal avgVolume20,
        decimal priceChangePct)
    {
        var emaTrendScore = isCall
            ? (ema20 > ema50 ? Max : 0m)
            : (ema20 < ema50 ? Max : 0m);

        var ema5TrendScore = isCall
            ? (ema20_5m > 0 && ema20_5m > ema50_5m ? Max : 0m)
            : (ema20_5m > 0 && ema20_5m < ema50_5m ? Max : 0m);

        decimal rsiScore = isCall
            ? (rsi >= 60m ? Max : rsi >= 52m ? Max * 0.6m : 0m)
            : (rsi <= 40m ? Max : rsi <= 48m ? Max * 0.6m : 0m);

        // PCR = Put OI / Call OI (PcrService) — high PCR (more puts) read as bearish tone, favors PE.
        decimal pcrScore = isCall
            ? (pcrValue < 0.8m ? Max : pcrValue < 1.0m ? Max * 0.5m : 0m)
            : (pcrValue > 1.2m ? Max : pcrValue > 1.0m ? Max * 0.5m : 0m);

        // Direction-agnostic — reuses MarketRegimeService's per-symbol High/Extreme ATR bands
        // rather than duplicating thresholds; rewards being inside the healthy (non-low, non-extreme) band.
        var atrThreshold = MarketRegimeService.GetAtrThreshold(symbol);
        var atrMaxThreshold = MarketRegimeService.GetAtrMaxThreshold(symbol);
        var atrScore = atrRaw >= atrThreshold && atrRaw < atrMaxThreshold ? Max : 0m;

        // Direction-agnostic — elevated index volume rewards conviction regardless of side.
        var volumeRatio = avgVolume20 > 0 ? currentVolume / avgVolume20 : 0m;
        var marketVolumeScore =
            volumeRatio >= 2m ? Max :
            volumeRatio >= 1.5m ? Max * 0.6m :
            volumeRatio >= 1m ? Max * 0.3m : 0m;

        var favorableChangePct = isCall ? priceChangePct : -priceChangePct;
        var priceChangeScore =
            favorableChangePct >= 0.5m ? Max :
            favorableChangePct >= 0.2m ? Max * 0.6m :
            favorableChangePct > 0m ? Max * 0.3m : 0m;

        return new MarketScoreBreakdown(
            emaTrendScore, ema5TrendScore, rsiScore, pcrScore, atrScore, marketVolumeScore, priceChangeScore);
    }
}
