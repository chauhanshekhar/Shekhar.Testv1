namespace Trading.Domain.Entities;

public class OptionChainData
{
    public long Id { get; set; }
    public string Symbol { get; set; } = "";
    public string TradingSymbol { get; set; } = "";   // Groww trading_symbol e.g. NIFTY26JUN1923550CE
    public decimal StrikePrice { get; set; }
    public string OptionType { get; set; } = "";

    // Price
    public decimal Ltp { get; set; }

    // Open interest & volume
    public long OpenInterest { get; set; }
    public long ChangeInOi { get; set; }
    public long Volume { get; set; }

    // IV
    public decimal ImpliedVolatility { get; set; }

    // Greeks
    public decimal Delta { get; set; }
    public decimal Gamma { get; set; }
    public decimal Theta { get; set; }
    public decimal Vega { get; set; }
    public decimal Rho { get; set; }

    // Expiry
    public string Expiry { get; set; } = "";
    public DateTime ExpiryDate { get; set; }

    // Underlying price at snapshot time
    public decimal UnderlyingLtp { get; set; }

    // Snapshot metadata
    public string SnapshotId { get; set; } = "";
    public DateTime CreatedOn { get; set; }
}
