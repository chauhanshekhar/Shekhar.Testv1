namespace Trading.Domain.Entities;

public class StrategyExecutionLog
{
    public long Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public string Symbol { get; set; } = "";
    public string SelectedStrategy { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal Score { get; set; }
}
