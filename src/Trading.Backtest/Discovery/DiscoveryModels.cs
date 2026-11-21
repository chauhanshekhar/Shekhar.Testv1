namespace Trading.Backtest.Discovery;

public enum DiscoverySide { Call, Put }

public sealed record DiscoveryRule(
    string Name,
    DiscoverySide Side,
    bool RequireFiveMinuteTrend,
    decimal? RsiLow,
    decimal? RsiHigh,
    decimal MinAtrPct,
    decimal? PcrThreshold);

public sealed record DiscoveryTrade(
    string Symbol, DateTime EntryIst, DateTime ExitIst, DiscoverySide Side,
    string GapGroup, string SessionGroup, string RuleName,
    decimal Entry, decimal Exit, decimal Target, decimal Stop,
    string Result, decimal RMultiple);

public sealed class DiscoverySummary
{
    public string Symbol { get; init; } = "";
    public string RuleName { get; init; } = "";
    public string Side { get; init; } = "";
    public string GapGroup { get; init; } = "";
    public string SessionGroup { get; init; } = "";
    public int Trades { get; set; }
    public int Targets { get; set; }
    public int Stops { get; set; }
    public int Neither { get; set; }
    public decimal GrossWinR { get; set; }
    public decimal GrossLossR { get; set; }
    public decimal NetR => GrossWinR - GrossLossR;
    public decimal WinRatePct => Trades == 0 ? 0 : 100m * Targets / Trades;
    public decimal ProfitFactor => GrossLossR == 0 ? (GrossWinR > 0 ? 99m : 0m) : GrossWinR / GrossLossR;
}

public sealed record DiscoveryOptions(
    int ForwardMinutes = 15,
    decimal TargetAtr = 1.0m,
    decimal StopAtr = 0.7m,
    int MinimumTrades = 20);
