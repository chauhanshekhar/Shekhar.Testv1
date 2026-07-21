namespace Trading.Domain.Entities;

/// <summary>
/// One row per (Strategy, MarketState, Expiry, Hour) bucket — recomputed after every completed
/// (non-partial) trade by StrategyPerformanceService. Read by StrategySelectorService to weight
/// strategy choice once a bucket has enough trades (see TradingSettings.MinTradesForHistoricalWeight).
/// </summary>
public class StrategyPerformance
{
    public long Id { get; set; }

    public string Strategy { get; set; } = "";
    public string MarketState { get; set; } = "";
    public bool Expiry { get; set; }
    public int Hour { get; set; }

    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal Expectancy { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal NetPnL { get; set; }
    public decimal MaxDrawdown { get; set; }

    public int Trades { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }

    public DateTime LastUpdated { get; set; }
}
