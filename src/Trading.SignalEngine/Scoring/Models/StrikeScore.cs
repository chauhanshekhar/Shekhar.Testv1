namespace Trading.SignalEngine.Scoring.Models;

public class StrikeScore
{
    public string Symbol { get; set; } = "";
    public string TradingSymbol { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public string OptionType { get; set; } = "";
    public decimal Ltp { get; set; }

    public decimal TrendScore { get; set; }
    public decimal OiScore { get; set; }
    public decimal VolumeScore { get; set; }
    public decimal GreeksScore { get; set; }
    public decimal LiquidityScore { get; set; }
    public decimal MomentumScore { get; set; }

    public decimal TotalScore =>
        TrendScore + OiScore + VolumeScore + GreeksScore + LiquidityScore + MomentumScore;

    // Raw market data — carried through for logging/persistence, not re-read from the chain.
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Theta { get; set; }
    public decimal Vega { get; set; }
    public long OpenInterest { get; set; }
    public long ChangeInOi { get; set; }
    public long Volume { get; set; }
}
