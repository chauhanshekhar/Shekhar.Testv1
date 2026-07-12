namespace Trading.Domain.Entities;

public class HeroZeroScore
{
    public long Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public string Symbol { get; set; } = "";
    public string TradingSymbol { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public string OptionType { get; set; } = "";

    public decimal TrendScore { get; set; }
    public decimal OiScore { get; set; }
    public decimal VolumeScore { get; set; }
    public decimal GreeksScore { get; set; }
    public decimal LiquidityScore { get; set; }
    public decimal MomentumScore { get; set; }
    public decimal TotalScore { get; set; }
}
