namespace Trading.Domain.Entities;

public class BacktestResult
{
    public long Id { get; set; }
    public int TotalTrades { get; set; }

    public int WinningTrades { get; set; }

    public int LosingTrades { get; set; }

    public decimal WinRate { get; set; }

    public decimal NetProfit { get; set; }

    public decimal MaxDrawdown { get; set; }

    public decimal ProfitFactor { get; set; }

    public decimal AveragePnL { get; set; }
}