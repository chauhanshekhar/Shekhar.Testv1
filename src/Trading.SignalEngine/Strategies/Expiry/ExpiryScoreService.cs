using Trading.Domain;
using Trading.Domain.Entities;
using Trading.SignalEngine.Scoring.Services;

namespace Trading.SignalEngine.Strategies.Expiry;

/// <summary>
/// Scores every CE and PE for expiry day. Reuses TrendScoreService/VolumeScoreService/
/// MomentumScoreService (rescaled via their maxScore param) — only Gamma and OI Shift
/// are expiry-specific, since neither fits the existing Hero Zero scorers' shape.
/// </summary>
public class ExpiryScoreService
{
    private const decimal TrendMax = 10m;
    private const decimal VolumeMax = 25m;
    private const decimal GammaMax = 25m;
    private const decimal OiShiftMax = 25m;

    private readonly TrendScoreService _trend;
    private readonly MomentumScoreService _momentum;
    private readonly VolumeScoreService _volume;

    public ExpiryScoreService(
        TrendScoreService trend,
        MomentumScoreService momentum,
        VolumeScoreService volume)
    {
        _trend = trend;
        _momentum = momentum;
        _volume = volume;
    }

    public (List<ExpiryStrikeScore> CeScores, List<ExpiryStrikeScore> PeScores) Score(
        string symbol,
        List<OptionChainData> chain,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal priceChangePct,
        List<Candle> candles5m)
    {
        var ceRows = chain.Where(o => o.OptionType == TradingConst.OptionType.CE).ToList();
        var peRows = chain.Where(o => o.OptionType == TradingConst.OptionType.PE).ToList();

        // OI Shift is a chain-wide fact (CE side building up while PE side unwinds, or vice
        // versa) — evaluated once from aggregate totals, not per individual strike.
        var totalCeOiChange = ceRows.Sum(o => o.ChangeInOi);
        var totalPeOiChange = peRows.Sum(o => o.ChangeInOi);

        return (
            ScoreSide(symbol, ceRows, true, ema20, ema50, ema20_5m, ema50_5m, priceChangePct, candles5m, totalCeOiChange, totalPeOiChange),
            ScoreSide(symbol, peRows, false, ema20, ema50, ema20_5m, ema50_5m, priceChangePct, candles5m, totalCeOiChange, totalPeOiChange));
    }

    private List<ExpiryStrikeScore> ScoreSide(
        string symbol,
        List<OptionChainData> rows,
        bool isCall,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal priceChangePct,
        List<Candle> candles5m,
        long totalCeOiChange,
        long totalPeOiChange)
    {
        if (rows.Count == 0)
            return [];

        var sortedVolumes = rows.Select(o => o.Volume).OrderBy(v => v).ToList();
        var medianVolume = Median(sortedVolumes);
        var meanGamma = rows.Average(o => o.Gamma);

        var oiShiftScore = ScoreOiShift(isCall, totalCeOiChange, totalPeOiChange);
        var premiumMomentumScore = _momentum.Score(isCall, priceChangePct, candles5m);

        return rows.Select(o => new ExpiryStrikeScore
        {
            Symbol = symbol,
            TradingSymbol = o.TradingSymbol,
            StrikePrice = o.StrikePrice,
            OptionType = o.OptionType,
            Ltp = o.Ltp,
            Delta = o.Delta,

            TrendScore = _trend.Score(isCall, o.UnderlyingLtp, ema20, ema50, ema20_5m, ema50_5m, TrendMax),
            VolumeScore = _volume.Score(o.Volume, medianVolume, Percentile(sortedVolumes, o.Volume), VolumeMax),
            GammaScore = ScoreGamma(o.Gamma, meanGamma),
            OiShiftScore = oiShiftScore,
            PremiumMomentumScore = premiumMomentumScore
        }).ToList();
    }

    /// <summary>
    /// Pure OI-vs-OI comparison across sides — deliberately not routed through
    /// OiAnalysisService, whose IsLongBuildUp/IsLongUnwinding couple OI direction to
    /// PRICE direction, not to the opposite side's OI direction as this rule needs.
    /// </summary>
    private static decimal ScoreOiShift(bool isCall, long totalCeOiChange, long totalPeOiChange)
    {
        if (isCall)
            return totalCeOiChange > 0 && totalPeOiChange < 0 ? OiShiftMax : 0m;

        return totalPeOiChange > 0 && totalCeOiChange < 0 ? OiShiftMax : 0m;
    }

    private static decimal ScoreGamma(decimal gamma, decimal meanGammaForSide)
    {
        if (meanGammaForSide <= 0) return 0m;

        var ratio = gamma / meanGammaForSide;
        return ratio > 1.5m ? GammaMax : ratio > 0.75m ? GammaMax * 0.6m : 0m; // High / Medium(15 of 25) / Low
    }

    private static decimal Median(List<long> sortedValues)
    {
        if (sortedValues.Count == 0) return 0m;

        var mid = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 0
            ? (sortedValues[mid - 1] + sortedValues[mid]) / 2m
            : sortedValues[mid];
    }

    private static decimal Percentile(List<long> sortedValues, long value)
    {
        if (sortedValues.Count == 0) return 0m;

        var countAtOrBelow = sortedValues.Count(v => v <= value);
        return (decimal)countAtOrBelow / sortedValues.Count;
    }
}
