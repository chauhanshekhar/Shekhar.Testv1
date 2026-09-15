namespace Trading.Domain.Entities;

/// <summary>
/// Forward-looking outcome of one <see cref="IndicatorSnapshot"/>: did the INDEX move +20 points
/// in the snapshot's own direction (TARGET) or -20 points against it (SL) first, within 5, 10 and
/// 15 minutes.
///
/// ANALYTICS ONLY. Never read by signal generation, order placement or exit management. Exists so
/// "which market conditions are historically profitable" can be answered from data instead of
/// assumption.
///
/// IMPORTANT — 20 points means INDEX points, measured on IndicatorSnapshot.Ltp. It is NOT option
/// premium: OptionLtp/OptionSL/OptionT1/OptionT2 and Delta play no part in this table. Option
/// profitability is a separate analysis and the live option target logic is untouched.
///
/// Separate table rather than more columns on IndicatorSnapshots (which is already wide and is
/// written once per second per symbol on the live path), and distinct from SignalOutcome, which
/// keys off a traded SignalId and measures option P&amp;L on trades that actually happened.
/// </summary>
public class IndicatorSnapshotOutcome
{
    public long Id { get; set; }

    /// <summary>The snapshot this outcome describes. Unique — one outcome row per snapshot.</summary>
    public long IndicatorSnapshotId { get; set; }

    public string Symbol { get; set; } = "";

    /// <summary>CALL (from TrendDirection=Bullish) or PUT (from TrendDirection=Bearish).</summary>
    public string Direction { get; set; } = "";

    /// <summary>Index LTP at the snapshot — the point all moves are measured from.</summary>
    public decimal EntryPrice { get; set; }

    /// <summary>Stored per row so the row stays self-describing if the constant is ever changed.</summary>
    public decimal TargetPoints { get; set; }
    public decimal StopLossPoints { get; set; }

    // ── Window results ───────────────────────────────────────────────────────────────────────
    // NULL means "not calculated yet", never "no hit" — a snapshot whose window has not finished
    // elapsing must not be recorded as NONE. See CompletedWindowMinutes.

    public bool? Target20Hit5Min { get; set; }
    public bool? SL20Hit5Min { get; set; }
    public string? FirstHitResult5Min { get; set; }      // TARGET / SL / NONE

    public bool? Target20Hit10Min { get; set; }
    public bool? SL20Hit10Min { get; set; }
    public string? FirstHitResult10Min { get; set; }

    public bool? Target20Hit15Min { get; set; }
    public bool? SL20Hit15Min { get; set; }
    public string? FirstHitResult15Min { get; set; }

    // ── Excursions, in directional points (favourable is positive for both CALL and PUT) ─────
    // Measured across the WHOLE window, not truncated at the first barrier hit, so the analysis
    // can see how much room the setup actually offered.

    public decimal? MaxFavorableMove5 { get; set; }
    public decimal? MaxAdverseMove5 { get; set; }

    public decimal? MaxFavorableMove10 { get; set; }
    public decimal? MaxAdverseMove10 { get; set; }

    public decimal? MaxFavorableMove15 { get; set; }
    public decimal? MaxAdverseMove15 { get; set; }

    /// <summary>
    /// Seconds from the snapshot to the FIRST barrier touch (target or stop), null when neither
    /// was reached inside 15 minutes. Combined with FirstHitResult15Min this gives time-to-target
    /// and time-to-stop without storing a second timestamp.
    /// </summary>
    public int? SecondsToFirstHit { get; set; }

    /// <summary>
    /// How far the calculation has progressed: 0 (pending), 5, 10 or 15. Drives incremental
    /// processing — a snapshot only 6 minutes old gets its 5-minute window filled and is revisited
    /// later for 10 and 15 — and keeps "not yet due" cleanly distinguishable from "no hit".
    /// </summary>
    public int CompletedWindowMinutes { get; set; }

    /// <summary>
    /// Set when a future price series was found to be incomplete or contradictory (see
    /// OutcomeBarrierCalculator). Null on healthy rows. Recorded rather than silently dropped.
    /// </summary>
    public string? Anomaly { get; set; }

    public DateTime CreatedOn { get; set; }

    /// <summary>Last time any window on this row was calculated.</summary>
    public DateTime? CalculatedOn { get; set; }
}

/// <summary>First-hit classification. Fixed vocabulary — no ambiguous values.</summary>
public static class OutcomeResult
{
    public const string Target = "TARGET";
    public const string StopLoss = "SL";
    public const string None = "NONE";
}
