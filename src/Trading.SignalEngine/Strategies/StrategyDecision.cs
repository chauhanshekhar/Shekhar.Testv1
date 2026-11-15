using Trading.SignalEngine.Services;

namespace Trading.SignalEngine.Strategies;

public enum SelectedStrategy
{
    None,
    Directional,
    HeroZero
}

public class StrategyDecision
{
    public SelectedStrategy Strategy { get; set; }
    public int Priority { get; set; }
    public string Reason { get; set; } = "";
    public MarketState MarketState { get; set; }

    // Set only when the historical override in StrategySelectorService actually consulted
    // StrategyPerformance for this decision (null otherwise) — surfaced for explainable logging.
    public decimal? HistoricalWinRate { get; set; }
    public int? HistoricalTrades { get; set; }
}
