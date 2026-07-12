using Trading.Domain;

namespace Trading.Domain.Entities;

public class SignalOutcome
{
    public long   Id             { get; set; }
    public long   SignalId       { get; set; }
    public string Symbol         { get; set; } = "";
    public string StrategyType   { get; set; } = "";
    public string Result         { get; set; } = "";   // WIN / LOSS / BREAKEVEN
    public string ExitReason     { get; set; } = "";
    public decimal EntryPrice    { get; set; }         // option entry LTP
    public decimal ExitPrice     { get; set; }
    public decimal PnL           { get; set; }
    public decimal Mfe           { get; set; }         // max favourable excursion (₹)
    public decimal Mae           { get; set; }         // max adverse excursion (₹)
    public decimal MfePct        { get; set; }         // Mfe / EntryPrice * 100
    public decimal MaePct        { get; set; }
    public decimal Confidence    { get; set; }
    public int    HoldingMinutes { get; set; }
    public DateTime CreatedOn    { get; set; } = IstClock.Now;
}
