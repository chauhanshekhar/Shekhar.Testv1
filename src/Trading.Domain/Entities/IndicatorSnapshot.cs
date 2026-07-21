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

    public DateTime CreatedOn { get; set; }
}
