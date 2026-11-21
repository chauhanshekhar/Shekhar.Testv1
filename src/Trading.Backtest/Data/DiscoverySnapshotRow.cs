namespace Trading.Backtest.Data;

/// <summary>Raw historical indicator values. Deliberately excludes all prior signal/selector/gate decisions.</summary>
public sealed class DiscoverySnapshotRow
{
    public string Symbol { get; set; } = "";
    public DateTime SnapshotTime { get; set; } // IST in IndicatorSnapshots
    public decimal Ltp { get; set; }
    public decimal DayOpen { get; set; }
    public decimal YesterdayClose { get; set; }
    public decimal Ema20 { get; set; }
    public decimal Ema50 { get; set; }
    public decimal Ema20_5m { get; set; }
    public decimal Ema50_5m { get; set; }
    public decimal Rsi { get; set; }
    public decimal? Rsi5m { get; set; }
    public decimal AtrRaw { get; set; }
    public decimal AtrPct { get; set; }
    public decimal Pcr { get; set; }
    public decimal? PcrFullChain { get; set; }
    public decimal? Vwap { get; set; }
    public decimal PriceChangePct { get; set; }
}
