using System.Globalization;
using System.Text;
using Trading.Domain.Entities;

namespace Trading.Backtest.Simulation;

/// <summary>One live signal compared with the per-second option path ExitWorker recorded (SignalOptionTicks).</summary>
public sealed record TickCheckRow(
    long SignalId, string Symbol, string LiveExitReason, DateTime? LiveExitTime, string ReplayExitReason,
    double? ReplayPnlRs, decimal? LivePnlRs, DateTime? FirstStopTouch, string TouchedStop,
    int QuoteTicks, int ChainTicks, string Verdict);

/// <summary>
/// Research only, read-only. Asks: for a live signal the replay also reproduced, did the 1-minute premium path hide a stop that the
/// per-second path shows was touched? It never changes simulated results and never feeds back into any gate or setting.
///
/// Only QUOTE ticks count as evidence: CHAIN ticks come from the 30-second chain snapshot and can be stale.
/// </summary>
public static class TickPathCheck
{
    public const string MinuteMissedStop = "MINUTE_PATH_MISSED_STOP";
    public const string Same = "SAME";
    public const string TicksInsufficient = "TICKS_INSUFFICIENT";

    /// <summary>Fewer QUOTE ticks than this (about 30 s of a position) cannot say anything about a stop.</summary>
    public const int MinQuoteTicks = 30;

    public static TickCheckRow Evaluate(
        long signalId, string symbol, string liveExitReason, DateTime? liveExitTime, string replayExitReason,
        double? replayPnlRs, decimal? livePnlRs, IReadOnlyList<SignalOptionTick> ticks)
    {
        var quotes = ticks
            .Where(t => t.Source == SignalOptionTickSource.Quote && t.OptionLtp > 0)
            .OrderBy(t => t.TickTime)
            .ToList();
        var chainTicks = ticks.Count(t => t.Source == SignalOptionTickSource.Chain);

        DateTime? touchTime = null;
        var touched = "";
        foreach (var t in quotes)
        {
            // The stop that applied to THIS tick's decision: the protected stop once it rests, else the option stop.
            if (t.ProtectedSL > 0 && t.OptionLtp <= t.ProtectedSL) { touchTime = t.TickTime; touched = "ProtectedSL"; break; }
            if (t.OptionSL > 0 && t.OptionLtp <= t.OptionSL) { touchTime = t.TickTime; touched = "OptionSL"; break; }
        }

        var verdict = Decide(quotes.Count, touchTime is not null, liveExitReason, replayExitReason);
        return new TickCheckRow(signalId, symbol, liveExitReason, liveExitTime, replayExitReason, replayPnlRs, livePnlRs,
            touchTime, touched, quotes.Count, chainTicks, verdict);
    }

    /// <summary>
    /// TICKS_INSUFFICIENT: too few QUOTE ticks. MINUTE_PATH_MISSED_STOP: a QUOTE tick touched a stop, live exited on a stop,
    /// and the replay exited for a different reason. Anything else is SAME (the ticks give no reason to doubt the replay).
    /// </summary>
    public static string Decide(int quoteTicks, bool stopTouched, string liveExitReason, string replayExitReason)
    {
        if (quoteTicks < MinQuoteTicks)
            return TicksInsufficient;

        var liveStop = liveExitReason.Contains("SL", StringComparison.OrdinalIgnoreCase);
        var sameReason = string.Equals(liveExitReason, replayExitReason, StringComparison.OrdinalIgnoreCase);
        return stopTouched && liveStop && !sameReason ? MinuteMissedStop : Same;
    }

    public static string ToCsv(IReadOnlyList<TickCheckRow> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("SignalId,Symbol,LiveExitReason,LiveExitTime,ReplayExitReason,ReplayPnlRs,LivePnlRs,FirstStopTouch,TouchedStop,QuoteTicks,ChainTicks,Verdict");
        foreach (var r in rows)
            sb.AppendLine(string.Join(',',
                r.SignalId.ToString(inv), r.Symbol, r.LiveExitReason, r.LiveExitTime?.ToString("yyyy-MM-dd HH:mm:ss", inv) ?? "",
                r.ReplayExitReason, r.ReplayPnlRs?.ToString("0.00", inv) ?? "", r.LivePnlRs?.ToString("0.00", inv) ?? "",
                r.FirstStopTouch?.ToString("yyyy-MM-dd HH:mm:ss", inv) ?? "", r.TouchedStop,
                r.QuoteTicks.ToString(inv), r.ChainTicks.ToString(inv), r.Verdict));
        return sb.ToString();
    }

    public static string Summary(IReadOnlyList<TickCheckRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Tick check: replay exits vs the per-second option path");
        sb.AppendLine();
        sb.AppendLine($"Live signals with recorded ticks and a replay match: {rows.Count}");
        foreach (var g in rows.GroupBy(r => r.Verdict).OrderBy(g => g.Key))
            sb.AppendLine($"- {g.Key}: {g.Count()}");
        sb.AppendLine();
        sb.AppendLine("Only QUOTE ticks are evidence (CHAIN ticks can be ~30 s stale). Covers signals live held; says nothing about replay-only entries.");
        return sb.ToString();
    }
}
