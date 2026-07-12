namespace Trading.Domain.Entities;

/// <summary>
/// Stores the latest computed value of a technical indicator per symbol.
/// Updated by EmaWorker every cycle so the API and dashboard
/// can read current indicator values without re-calculating on every request.
/// </summary>
public class SymbolIndicator
{
    public long Id { get; set; }

    public string Symbol { get; set; } = "";

    /// <summary>e.g. "EMA20", "EMA50"</summary>
    public string IndicatorName { get; set; } = "";

    public decimal Value { get; set; }

    public DateTime UpdatedOn { get; set; }
}
