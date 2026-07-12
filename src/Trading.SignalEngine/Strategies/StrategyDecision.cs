namespace Trading.SignalEngine.Strategies;

public enum SelectedStrategy
{
    None,
    Directional,
    HeroZero,
    Expiry
}

public class StrategyDecision
{
    public SelectedStrategy Strategy { get; set; }
    public int Priority { get; set; }
    public string Reason { get; set; } = "";
}
