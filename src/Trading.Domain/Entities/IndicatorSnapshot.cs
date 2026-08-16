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

    public decimal Rsi { get; set; }
    public decimal AtrRaw { get; set; }
    public decimal AtrPct { get; set; }
    public decimal Pcr { get; set; }
    public decimal Vwap { get; set; }

    public decimal CurrentVolume { get; set; }
    public decimal AvgVolume20 { get; set; }
    public decimal VolumeRatio { get; set; }
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

    // Score breakdown from SignalEngineService's scored path (see SignalScore) — populated only
    // when EnableSignalScoring is on for the symbol; null under the legacy AND-gated path. Carries
    // the winning candidate's breakdown when a signal was generated, or the higher of the two
    // (still-blocked) candidates' breakdowns when neither cleared the threshold, so future
    // backtesting/tuning can see exactly which component(s) were weak.
    public string? ScoreDirection { get; set; }
    public int? ScoreTotal { get; set; }
    public int? ScoreTrend { get; set; }
    public int? ScoreEma20 { get; set; }
    public int? ScoreEma50 { get; set; }
    public int? ScoreRsi { get; set; }
    public int? ScorePriceChange { get; set; }
    public int? ScoreCandle { get; set; }
    public int? ScoreBody { get; set; }
    public int? ScoreAtr { get; set; }
    public int? ScorePcr { get; set; }
    public int? ScoreOi { get; set; }
    public int? ScoreConfidence { get; set; }

    public DateTime CreatedOn { get; set; }
}
