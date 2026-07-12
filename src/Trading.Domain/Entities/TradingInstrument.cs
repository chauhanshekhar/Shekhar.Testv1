using Trading.Domain;
namespace Trading.Domain.Entities;

public class TradingInstrument
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";   // e.g. NIFTY, BANKNIFTY
    public string Exchange { get; set; } = "NSE"; // NSE or BSE
    public bool IsActive { get; set; } = true;
    public string? Expiry { get; set; }          // yyyy-MM-dd — set by OptionChainWorker
    public int LotSize { get; set; } = 1;        // contracts per lot (SEBI-defined)
    public int StrikeInterval { get; set; } = 50;    // gap between strike prices
    public int IndexStopPoints { get; set; } = 50;   // index-level SL distance (BANKNIFTY/SENSEX=100, others=50)
    public DateTime UpdatedOn { get; set; } = IstClock.Now;
}
