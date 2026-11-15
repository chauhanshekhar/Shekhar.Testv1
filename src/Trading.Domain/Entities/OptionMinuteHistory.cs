namespace Trading.Domain.Entities;

/// <summary>
/// One row per option leg per minute, for the ATM±5 strikes OptionChainWorker already fetches.
/// Analysis/backtesting only — never read by signal generation, order placement or exit management.
///
/// Why a separate table: OptionChainData is pruned to the last 2 hours on every save so the live
/// exit path's "latest chain" query stays fast; without this copy no premium or delta history
/// survives, and backtests have to assume premium = 0.5 × index move. Kept 90 days
/// (TickRetentionWorker).
/// </summary>
public class OptionMinuteHistory
{
    public long Id { get; set; }

    /// <summary>Index symbol (NIFTY, BANKNIFTY, SENSEX).</summary>
    public string Symbol { get; set; } = "";

    /// <summary>IST, truncated to the minute. First chain fetch of that minute wins.</summary>
    public DateTime MinuteTime { get; set; }

    public string TradingSymbol { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public string OptionType { get; set; } = "";      // CE / PE
    public DateTime ExpiryDate { get; set; }

    public decimal Ltp { get; set; }
    public decimal Delta { get; set; }
    public decimal ImpliedVolatility { get; set; }
    public long OpenInterest { get; set; }
    public long ChangeInOi { get; set; }
    public decimal UnderlyingLtp { get; set; }

    public DateTime CreatedOn { get; set; }
}
