namespace Trading.Domain.Entities;

// Read-only snapshot of an open paper trade enriched with live market price.
// Never persisted — computed on demand per request.
public class PaperPositionSnapshot
{
    public long Id { get; init; }
    public string Symbol { get; init; } = "";
    public string OptionSymbol { get; init; } = "";
    public string Side { get; init; } = "";
    public int Quantity { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopLoss { get; init; }
    public decimal Target1 { get; init; }
    public decimal Target2 { get; init; }
    public decimal CurrentLtp { get; init; }   // live price from market feed
    public decimal UnrealisedPnL { get; init; }  // (currentLtp - entryPrice) * qty
    public decimal PnLPercent { get; init; }   // unrealisedPnL / (entryPrice * qty) * 100
    public string Status { get; init; } = "OPEN";
    public DateTime CreatedOn { get; init; }
}
