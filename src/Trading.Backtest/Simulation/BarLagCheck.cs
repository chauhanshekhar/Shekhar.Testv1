using System.Globalization;
using System.Text;
using Trading.Backtest.Data;

namespace Trading.Backtest.Simulation;

/// <summary>One replay entry blocked by StopNoiseGate, and how much a not-yet-final newest 5m bar could have changed that.</summary>
public sealed record BarLagRow(
    string Symbol, DateTime MinuteIst, long? LiveSignalId, string LiveOutcome,
    double StopPoints, double AvgRange, double Required, double Shortfall, double MaxRequiredDrop, string Verdict);

/// <summary>
/// Research only, read-only. Live reads the stored 5m rows at the signal second; the newest "closed" bar may still be missing its last minute,
/// while the replay always sees the final bar. This measures the EXPOSURE: could a smaller newest bar have turned a replay block into a pass?
///
/// The newest of the N bars contributes range/N to the average, so even if its range were 0 the required stop drops by at most
/// ratio x newestRange / N. A block whose shortfall (required - stop) exceeds that is FLIP_IMPOSSIBLE; a smaller shortfall is FLIP_POSSIBLE.
/// It is an upper bound on lag sensitivity, not a measured live value - the STOP_NOISE_EVAL log line gives the real values.
/// It never changes simulated results and never feeds back into any gate or setting.
/// </summary>
public static class BarLagCheck
{
    public const string FlipPossible = "FLIP_POSSIBLE";
    public const string FlipImpossible = "FLIP_IMPOSSIBLE";
    public const string NotBlocked = "NOT_BLOCKED";
    public const string NoData = "NO_DATA";

    public static BarLagRow Evaluate(
        string symbol, DateTime minuteIst, long? liveSignalId, string liveOutcome,
        Bar[]? bars5m, int closed5m, double stopPoints, int bars, double ratio)
    {
        bars = Math.Max(1, bars);
        if (bars5m is null || closed5m < bars || closed5m > bars5m.Length)
            return new BarLagRow(symbol, minuteIst, liveSignalId, liveOutcome, stopPoints, 0, 0, 0, 0, NoData);

        double sum = 0;
        for (var i = closed5m - bars; i < closed5m; i++)
            sum += bars5m[i].High - bars5m[i].Low;

        var avg = sum / bars;
        var required = avg * ratio;
        var shortfall = required - stopPoints;
        var newest = bars5m[closed5m - 1];
        var maxDrop = ratio * (newest.High - newest.Low) / bars;

        var verdict = shortfall <= 0 ? NotBlocked : shortfall <= maxDrop ? FlipPossible : FlipImpossible;
        return new BarLagRow(symbol, minuteIst, liveSignalId, liveOutcome, stopPoints, avg, required, shortfall, maxDrop, verdict);
    }

    public static string ToCsv(IReadOnlyList<BarLagRow> rows)
    {
        var sb = new StringBuilder("Symbol,MinuteIst,LiveSignalId,LiveOutcome,StopPoints,AvgRange,Required,Shortfall,MaxRequiredDrop,Verdict\n");
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", r.Symbol, r.MinuteIst.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                r.LiveSignalId?.ToString(CultureInfo.InvariantCulture) ?? "", r.LiveOutcome,
                F(r.StopPoints), F(r.AvgRange), F(r.Required), F(r.Shortfall), F(r.MaxRequiredDrop), r.Verdict));
        return sb.ToString();
    }

    public static string Summary(IReadOnlyList<BarLagRow> rows)
    {
        var sb = new StringBuilder("# Stop-noise gate: sensitivity to a not-yet-final newest 5m bar\n\n");
        sb.AppendLine("Upper bound on exposure, not a measured live value. See Trading.Backtest/Simulation/BarLagCheck.cs.\n");
        sb.AppendLine($"Replay entries blocked by StopNoiseGate: {rows.Count(r => r.Verdict != NotBlocked && r.Verdict != NoData)}");
        sb.AppendLine($"- {FlipPossible}: {rows.Count(r => r.Verdict == FlipPossible)}");
        sb.AppendLine($"- {FlipImpossible}: {rows.Count(r => r.Verdict == FlipImpossible)}");
        sb.AppendLine($"- {NoData}: {rows.Count(r => r.Verdict == NoData)}\n");
        var live = rows.Where(r => r.LiveSignalId is not null && r.Verdict is FlipPossible or FlipImpossible).ToList();
        sb.AppendLine($"Of those, live actually took {live.Count} (LIVE_ONLY): {live.Count(r => r.Verdict == FlipPossible)} flip-possible, {live.Count(r => r.Verdict == FlipImpossible)} flip-impossible.");
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
