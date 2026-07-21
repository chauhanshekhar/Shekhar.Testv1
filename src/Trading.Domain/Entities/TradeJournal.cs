namespace Trading.Domain.Entities;

public class TradeJournal
{
    public long Id { get; set; }

    public string Symbol { get; set; } = "";

    public string TradeType { get; set; } = "";

    public decimal EntryPrice { get; set; }

    public decimal ExitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal ProfitLoss { get; set; }

    public DateTime TradeDate { get; set; }

    public string Remarks { get; set; } = "";

    // Structured FK — replaces Remarks-based signal lookup
    public long? SignalId { get; set; }
    public long? EntryOrderId { get; set; }
    public long? ExitOrderId { get; set; }
    public bool IsPartial { get; set; }
    public string StrategyType { get; set; } = "";
    public string? ExitReason { get; set; }

    // Market context at entry
    public decimal SignalConfidence  { get; set; }
    public decimal RsiAtEntry        { get; set; }
    public decimal PcrAtEntry        { get; set; }
    public decimal DeltaAtEntry      { get; set; }
    public decimal ChangeInOiAtEntry { get; set; }

    // Strategy/market context at entry — for StrategyPerformance bucketing.
    public string SelectedStrategy { get; set; } = "";
    public string MarketState { get; set; } = "";
    public bool IsExpiryDay { get; set; }
    public int EntryHour { get; set; }
}