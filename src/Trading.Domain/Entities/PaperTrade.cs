using Trading.Domain;
namespace Trading.Domain.Entities;

public class PaperTrade
{
    public long Id { get; set; }

    public long? SignalId { get; set; }

    public string Symbol { get; set; } = "";

    public string OptionSymbol { get; set; } = "";

    public string Side { get; set; } = "";          // BUY / SELL

    public string StrategyType { get; set; } = TradingConst.StrategyType.Single;

    // Leg2 — populated for spreads; empty for single-leg trades
    public string Leg2OptionSymbol { get; set; } = "";
    public decimal Leg2EntryPrice { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal EntryPrice { get; set; }

    public decimal StopLoss { get; set; }

    public decimal Target1 { get; set; }

    public decimal Target2 { get; set; }

    public decimal ExitPrice { get; set; }

    public decimal ProfitLoss { get; set; }

    public string Status { get; set; } = "OPEN";   // OPEN / CLOSED / SL_HIT / TARGET_HIT

    public string? CloseReason { get; set; }        // MANUAL / SL_HIT / TARGET_HIT / AUTO

    public DateTime CreatedOn { get; set; } = IstClock.Now;

    public DateTime? ClosedOn { get; set; }
}
