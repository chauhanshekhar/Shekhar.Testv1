namespace Trading.Domain.Entities;

/// <summary>
/// Audit-only — one row per actual ATR-based trailing-SL move (ExitWorker.UpdateTrailingSL).
/// Lets you reconstruct exactly why Signal.OptionSL moved from one value to another (which
/// ATR/delta/distance/step-cap produced it) without ExitWorker keeping its own history.
/// Never read by trading logic.
/// </summary>
public class TrailingStopLog
{
    public long Id { get; set; }
    public long SignalId { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime CreatedOn { get; set; }

    public decimal OldOptionSL { get; set; }
    public decimal NewOptionSL { get; set; }

    public decimal AtrRaw { get; set; }
    public decimal Delta { get; set; }
    public decimal OptionAtrDistance { get; set; }
    public decimal TargetSL { get; set; }
    public decimal MaxStep { get; set; }
    public decimal MfePrice { get; set; }
}
