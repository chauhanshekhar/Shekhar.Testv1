using Trading.Backtest.Data;
using Trading.Backtest.Entry;

namespace Trading.Backtest.Reporting;

/// <summary>
/// What a session did after it opened: continued with its gap, reversed against it, or went nowhere.
///
/// LOOK-AHEAD WARNING. Every value here is computed from the COMPLETED session - its close, high and
/// low. None of it is known at an entry minute, so this is a REPORTING dimension only. It must never
/// reach <see cref="EntryRules"/> or any gate: a rule that reads it would be trading on the future.
/// It lives in Reporting, not Entry, so that stays structurally obvious.
/// </summary>
public enum SessionPath
{
    /// <summary>Not classifiable (no previous close, or too few bars).</summary>
    Unknown,
    /// <summary>The session closed in the direction of its gap.</summary>
    Continuation,
    /// <summary>The session closed against its gap.</summary>
    Reversal,
    /// <summary>No decisive close either way - a narrow day, or a wide one that round-tripped.</summary>
    Range,
    /// <summary>Normal open (no meaningful gap) that closed up decisively.</summary>
    TrendUp,
    /// <summary>Normal open that closed down decisively.</summary>
    TrendDown,
}

/// <summary>One classified trading session.</summary>
public sealed record SessionProfile(
    string Symbol,
    DateOnly Date,
    double Open,
    double High,
    double Low,
    double Close,
    double? PrevClose,
    double? GapPct,
    double MoveFromOpenPct,
    double RangePct,
    OpeningType Opening,
    SessionPath Path,
    bool GapFilled,
    int Bars)
{
    /// <summary>The label used in reports, e.g. "GapDown/Continuation" or "Normal/TrendUp".</summary>
    public string DayType => $"{Opening}/{Path}";
}

/// <summary>Thresholds for <see cref="SessionClassifier"/>. All percentages of the session open.</summary>
public sealed record SessionThresholds(
    /// <summary>Opening gap that separates GapUp / Normal / GapDown. Same convention as the opening-type sweep.</summary>
    double GapMinPct = 0.30,
    /// <summary>A session whose entire high-low range is under this did nothing worth classifying.</summary>
    double RangeMaxPct = 0.40,
    /// <summary>
    /// |close - open| must be at least this fraction of the day's range to count as directional.
    /// A wide day that closed back near its open round-tripped, and is Range rather than a trend.
    /// </summary>
    double DecisiveFraction = 0.35);

public static class SessionClassifier
{
    /// <summary>
    /// Classifies every session present in <paramref name="bars"/>. Bars must be 1-minute and ordered;
    /// the previous session's close comes from the preceding day in the same series, so the first day
    /// in the data has no gap and is classified Unknown.
    /// </summary>
    public static IReadOnlyList<SessionProfile> Classify(
        string symbol, IReadOnlyList<Bar> bars, SessionThresholds? thresholds = null)
    {
        var t = thresholds ?? new SessionThresholds();
        var days = bars
            .OrderBy(b => b.OpenUtc)
            .GroupBy(b => DateOnly.FromDateTime(b.OpenUtc + SessionClock.IstOffset))
            .OrderBy(g => g.Key)
            .ToList();

        var profiles = new List<SessionProfile>(days.Count);
        double? prevClose = null;

        foreach (var day in days)
        {
            var session = day.ToList();
            var open = session[0].Open;
            var close = session[^1].Close;
            var high = session.Max(b => b.High);
            var low = session.Min(b => b.Low);

            // Too thin to say anything about, but it still sets the previous close for the next day.
            if (session.Count < 30 || open <= 0)
            {
                profiles.Add(new SessionProfile(symbol, day.Key, open, high, low, close, prevClose,
                    null, 0, 0, OpeningType.Any, SessionPath.Unknown, false, session.Count));
                prevClose = close;
                continue;
            }

            var gapPct = prevClose is > 0 ? (open - prevClose.Value) / prevClose.Value * 100.0 : (double?)null;
            var movePct = (close - open) / open * 100.0;
            var rangePct = (high - low) / open * 100.0;

            var opening = gapPct is null ? OpeningType.Any
                : gapPct >= t.GapMinPct ? OpeningType.GapUp
                : gapPct <= -t.GapMinPct ? OpeningType.GapDown
                : OpeningType.Normal;

            // Decisive = the close is far enough from the open, relative to how far the session
            // travelled. Both tests matter: a narrow day is Range however it closed, and a wide day
            // that came back to its open is Range too.
            var decisive = rangePct >= t.RangeMaxPct
                        && Math.Abs(movePct) >= t.DecisiveFraction * rangePct;

            SessionPath path;
            if (gapPct is null) path = SessionPath.Unknown;
            else if (!decisive) path = SessionPath.Range;
            else if (opening == OpeningType.Normal)
                path = movePct > 0 ? SessionPath.TrendUp : SessionPath.TrendDown;
            else
                path = Math.Sign(movePct) == Math.Sign(gapPct.Value)
                    ? SessionPath.Continuation
                    : SessionPath.Reversal;

            // Did price trade back through the previous close at any point (the gap "filled")?
            var gapFilled = prevClose is > 0 && gapPct is not null
                && (gapPct > 0 ? low <= prevClose.Value : high >= prevClose.Value);

            profiles.Add(new SessionProfile(symbol, day.Key, open, high, low, close, prevClose,
                gapPct, movePct, rangePct, opening, path, gapFilled, session.Count));
            prevClose = close;
        }

        return profiles;
    }
}
