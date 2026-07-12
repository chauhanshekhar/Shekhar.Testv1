using Trading.Domain.Entities;
using Trading.SignalEngine.Services;

namespace Trading.SignalEngine.Strategies;

public class StrategyContext
{
    public string Symbol { get; set; } = "";
    public decimal Ltp { get; set; }
    public decimal Ema20 { get; set; }
    public decimal Ema50 { get; set; }
    public decimal Rsi { get; set; }
    public decimal Pcr { get; set; }
    public decimal Atr { get; set; }
    public bool IsExpiryDay { get; set; }
    public bool IsHighImpactEvent { get; set; }
    public MarketRegime? Regime { get; set; }
    public List<OptionChainData> OptionChain { get; set; } = new();

    /// <summary>
    /// Top Hero Zero score across CE/PE for this cycle — computed once by the caller
    /// (StrategyManager, via HeroZeroScoreService + StrikeRankingService) since this class
    /// doesn't carry the extra inputs (Ema20_5m/Ema50_5m/PriceChangePct/Candles5m) that scoring needs.
    /// </summary>
    public decimal HeroZeroTopScore { get; set; }
}
