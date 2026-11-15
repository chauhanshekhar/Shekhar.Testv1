namespace Trading.Domain.Entities;

/// <summary>
/// Audit-only, per-cycle diagnostic row — one per successful MarketSnapshotBuilder.BuildAsync() call.
/// Never read by trading logic; exists purely so post-market analysis (ATR/RSI/EMA/regime/gate blocks/
/// strategy selection) doesn't require reading Docker logs.
/// </summary>
public class IndicatorSnapshot
{
    public long Id { get; set; }
    public DateTime SnapshotTime { get; set; }
    public string Symbol { get; set; } = "";

    public decimal Ltp { get; set; }

    public decimal DayOpen { get; set; }
    public decimal YesterdayClose { get; set; }

    public decimal Ema20 { get; set; }
    public decimal Ema50 { get; set; }
    public decimal Ema20_5m { get; set; }
    public decimal Ema50_5m { get; set; }

    /// <summary>1m EMA9 / EMA21. ANALYTICS ONLY - no gate, engine or rule reads them. Null on rows written before 2026-11-04.</summary>
    public decimal? Ema9 { get; set; }
    public decimal? Ema21 { get; set; }

    public decimal Rsi { get; set; }          // 1m — drives every RSI threshold in the system

    /// <summary>
    /// 5-minute RSI(14). ANALYTICS ONLY — no gate, engine or rule reads it. Present so the value
    /// a 5-minute chart displays can be compared against the 1m RSI the engine actually trades on.
    /// Null until 15 closed 5m bars exist (~75 minutes into the session), rather than the neutral
    /// 50 RsiService returns on short input, which would be indistinguishable from a real reading.
    /// </summary>
    public decimal? Rsi5m { get; set; }
    public decimal AtrRaw { get; set; }
    public decimal AtrPct { get; set; }
    public decimal Pcr { get; set; }
    public decimal? PcrFullChain { get; set; }
    public decimal? Vwap { get; set; }   // null when volume is unavailable (Groww sends none for indices)

    public decimal PriceChangePct { get; set; }

    public string TrendDirection { get; set; } = "";
    public string MarketRegime { get; set; } = "";

    // Filled in later by SignalWorker, once gates/strategy selection have run.
    public string MarketState { get; set; } = "";
    public string StrategySelected { get; set; } = "";
    public decimal Confidence { get; set; }
    public bool SignalGenerated { get; set; }
    public string? BlockedBy { get; set; }
    public string? BlockedReason { get; set; }

    // Candidate option leg at the moment a post-signal gate blocked it — null for pre-signal
    // gate blocks, since no strike is chosen yet at that point. Lets missed-opportunity analysis
    // correlate Delta/OI (not just underlying RSI/PriceChangePct) with what the trade would have done.
    public decimal? BlockedStrikePrice { get; set; }
    public string? BlockedOptionType { get; set; }
    public string? BlockedOptionSymbol { get; set; }
    public decimal? BlockedDelta { get; set; }
    public decimal? BlockedOptionLtp { get; set; }
    public decimal? BlockedChangeInOi { get; set; }

    public DateTime CreatedOn { get; set; }
}
