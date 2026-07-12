namespace Trading.SignalEngine.Scoring.Models;

public enum HeroZeroDecision
{
    NoTrade,
    BuyCe,
    BuyPe
}

public class HeroZeroResult
{
    public StrikeScore? HeroCe { get; set; }
    public StrikeScore? HeroPe { get; set; }
    public List<StrikeScore> CeRanking { get; set; } = new();
    public List<StrikeScore> PeRanking { get; set; } = new();
    public HeroZeroDecision Decision { get; set; } = HeroZeroDecision.NoTrade;
    public string Reason { get; set; } = "";
}
