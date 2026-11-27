namespace Trading.Domain.Entities;

/// <summary>
/// The option premium ExitWorker actually acted on, one row per open signal per cycle (~1/second
/// while a position is open). Analysis/backtesting only — never read by signal generation, order
/// placement or exit management.
///
/// Why: MarketTicks carries index symbols only and OptionMinuteHistory is one sample per minute, so
/// an intra-minute option dip (2026-11-25, signal 10449: PROTECTED_SL_HIT at 10:57:58 that a
/// minute-sample replay could not reproduce) could not be verified afterwards. These rows are the
/// price path the live exit ladder saw, so a replay can be checked against it.
///
/// OptionLtp is the value ResolveLtps returned. Source says where it came from: QUOTE (the per-cycle
/// Groww quote) or CHAIN (the 30-second chain snapshot fallback, possibly stale), NONE (no price).
/// OptionSL / ProtectedSL are the stops as they stood at the start of the cycle, i.e. what the
/// decision for this price was evaluated against. Kept 90 days (TickRetentionWorker).
/// </summary>
public class SignalOptionTick
{
    public long Id { get; set; }

    public long SignalId { get; set; }

    /// <summary>Index symbol (NIFTY, BANKNIFTY, SENSEX).</summary>
    public string Symbol { get; set; } = "";

    public string OptionSymbol { get; set; } = "";

    /// <summary>IST, the instant the cycle resolved the price.</summary>
    public DateTime TickTime { get; set; }

    public decimal OptionLtp { get; set; }

    /// <summary>QUOTE / CHAIN / NONE — see <see cref="SignalOptionTickSource"/>.</summary>
    public string Source { get; set; } = "";

    /// <summary>Index price the cycle used; 0 when the index tick was missing or stale.</summary>
    public decimal UnderlyingLtp { get; set; }

    public decimal OptionSL { get; set; }

    public decimal ProtectedSL { get; set; }

    /// <summary>Signal.Status at the start of the cycle.</summary>
    public string Status { get; set; } = "";
}

public static class SignalOptionTickSource
{
    public const string Quote = "QUOTE";
    public const string Chain = "CHAIN";
    public const string None = "NONE";
}
