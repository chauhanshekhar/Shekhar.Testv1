using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Scoring.Models;

namespace Trading.SignalEngine.Scoring.Services;

/// <summary>
/// Scores every CE and every PE in the option chain — orchestrates the six sub-scorers.
/// Pure computation; carries no state between calls.
/// </summary>
public class HeroZeroScoreService
{
    private readonly TrendScoreService _trend;
    private readonly OiScoreService _oi;
    private readonly VolumeScoreService _volume;
    private readonly GreeksScoreService _greeks;
    private readonly LiquidityScoreService _liquidity;
    private readonly MomentumScoreService _momentum;

    public HeroZeroScoreService(
        TrendScoreService trend,
        OiScoreService oi,
        VolumeScoreService volume,
        GreeksScoreService greeks,
        LiquidityScoreService liquidity,
        MomentumScoreService momentum)
    {
        _trend = trend;
        _oi = oi;
        _volume = volume;
        _greeks = greeks;
        _liquidity = liquidity;
        _momentum = momentum;
    }

    public (List<StrikeScore> CeScores, List<StrikeScore> PeScores) Score(
        string symbol,
        List<OptionChainData> chain,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal priceChangePct,
        List<Candle> candles5m,
        TradingSettings settings)
    {
        var ceRows = chain.Where(o => o.OptionType == TradingConst.OptionType.CE).ToList();
        var peRows = chain.Where(o => o.OptionType == TradingConst.OptionType.PE).ToList();

        return (
            ScoreSide(symbol, ceRows, true, ema20, ema50, ema20_5m, ema50_5m, priceChangePct, candles5m, settings),
            ScoreSide(symbol, peRows, false, ema20, ema50, ema20_5m, ema50_5m, priceChangePct, candles5m, settings));
    }

    private List<StrikeScore> ScoreSide(
        string symbol,
        List<OptionChainData> rows,
        bool isCall,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal priceChangePct,
        List<Candle> candles5m,
        TradingSettings settings)
    {
        if (rows.Count == 0)
            return [];

        var sortedVolumes = rows.Select(o => o.Volume).OrderBy(v => v).ToList();
        var medianVolume = Median(sortedVolumes);
        var avgGamma = rows.Average(o => o.Gamma);
        var avgAbsTheta = rows.Average(o => Math.Abs(o.Theta));

        return rows.Select(o => new StrikeScore
        {
            Symbol = symbol,
            TradingSymbol = o.TradingSymbol,
            StrikePrice = o.StrikePrice,
            OptionType = o.OptionType,
            Ltp = o.Ltp,
            Delta = o.Delta,
            Gamma = o.Gamma,
            Theta = o.Theta,
            Vega = o.Vega,
            OpenInterest = o.OpenInterest,
            ChangeInOi = o.ChangeInOi,
            Volume = o.Volume,

            TrendScore = _trend.Score(isCall, o.UnderlyingLtp, ema20, ema50, ema20_5m, ema50_5m),
            OiScore = _oi.Score(o.ChangeInOi, priceChangePct, isCall),
            VolumeScore = _volume.Score(o.Volume, medianVolume, Percentile(sortedVolumes, o.Volume)),
            GreeksScore = _greeks.Score(o.Delta, o.Gamma, o.Theta, avgGamma, avgAbsTheta),
            LiquidityScore = _liquidity.Score(symbol, o, settings),
            MomentumScore = _momentum.Score(isCall, priceChangePct, candles5m)
        }).ToList();
    }

    private static decimal Median(List<long> sortedValues)
    {
        if (sortedValues.Count == 0) return 0m;

        var mid = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 0
            ? (sortedValues[mid - 1] + sortedValues[mid]) / 2m
            : sortedValues[mid];
    }

    /// <summary>Fraction of same-type strikes with volume &lt;= this strike's — 1.0 means the highest.</summary>
    private static decimal Percentile(List<long> sortedValues, long value)
    {
        if (sortedValues.Count == 0) return 0m;

        var countAtOrBelow = sortedValues.Count(v => v <= value);
        return (decimal)countAtOrBelow / sortedValues.Count;
    }
}
