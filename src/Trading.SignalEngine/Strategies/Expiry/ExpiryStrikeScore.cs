namespace Trading.SignalEngine.Strategies.Expiry;

public class ExpiryStrikeScore
{
    public string Symbol { get; set; } = "";
    public string TradingSymbol { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public string OptionType { get; set; } = "";
    public decimal Ltp { get; set; }
    public decimal Delta { get; set; }

    public decimal GammaScore { get; set; }
    public decimal VolumeScore { get; set; }
    public decimal OiShiftScore { get; set; }
    public decimal PremiumMomentumScore { get; set; }
    public decimal TrendScore { get; set; }

    public decimal TotalScore =>
        GammaScore + VolumeScore + OiShiftScore + PremiumMomentumScore + TrendScore;
}
