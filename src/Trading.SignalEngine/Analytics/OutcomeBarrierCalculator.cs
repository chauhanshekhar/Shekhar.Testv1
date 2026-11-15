using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Analytics;

/// <summary>One future index price for the SAME symbol as the entry snapshot.</summary>
public readonly record struct FuturePoint(DateTime Time, decimal Ltp);

/// <summary>
/// Result for one time window. Every field is null when the window has not finished elapsing —
/// "not due yet" must never be recorded as NONE.
/// </summary>
public readonly record struct WindowOutcome(
    string? Result,
    bool? TargetHit,
    bool? SlHit,
    decimal? MaxFavorable,
    decimal? MaxAdverse)
{
    public static readonly WindowOutcome NotDue = new(null, null, null, null, null);
}

public readonly record struct OutcomeCalculation(
    WindowOutcome Window5,
    WindowOutcome Window10,
    WindowOutcome Window15,
    int CompletedWindowMinutes,
    string? Anomaly,
    /// <summary>Seconds from entry to the first barrier touch, null when neither was reached
    /// inside the 15-minute horizon. Feeds AvgTimeToTarget / AvgTimeToSL in the analytics.</summary>
    int? SecondsToFirstHit);

/// <summary>
/// First-hit barrier logic for a single snapshot: did the INDEX travel +20 points in the
/// snapshot's direction (TARGET) or -20 points against it (SL) FIRST, within 5, 10 and 15 minutes.
///
/// 20 points means INDEX points on IndicatorSnapshot.Ltp. Nothing here touches option premium,
/// delta, or any live order value.
///
/// Directional move — favourable is positive for both directions:
///     CALL: move = futureLtp - entryLtp
///     PUT : move = entryLtp - futureLtp
/// Barriers are INCLUSIVE: move >= +20 is TARGET, move &lt;= -20 is SL.
///
/// Pure and static: no DB, no clock of its own, no logging. Everything it needs is passed in, so
/// every branch is directly testable.
/// </summary>
public static class OutcomeBarrierCalculator
{
    /// <summary>The one and only barrier distance for this feature — +20 / -20 index points.</summary>
    public const decimal BarrierPoints = 20m;

    private static readonly int[] WindowMinutes = [5, 10, 15];

    /// <summary>
    /// <paramref name="future"/> must already be filtered to the entry's OWN symbol, exclude rows
    /// with no usable price, and be ordered by time ascending. <paramref name="asOf"/> decides
    /// which windows have finished elapsing.
    /// </summary>
    public static OutcomeCalculation Calculate(
        string direction,
        decimal entryPrice,
        DateTime entryTime,
        IReadOnlyList<FuturePoint> future,
        DateTime asOf,
        decimal barrierPoints = BarrierPoints)
    {
        var isCall = string.Equals(direction, TradingConst.SignalType.CALL, StringComparison.OrdinalIgnoreCase);

        // Walk forward once. The first barrier touched in the 15-minute window is, by definition,
        // also the first barrier touched in any shorter window that contains it — so each window's
        // independent answer is derivable from this single pass plus a time comparison. Re-walking
        // per window would give identical results at three times the cost.
        DateTime? firstHitTime = null;
        string? firstHitResult = null;
        string? anomaly = null;

        var horizon = entryTime.AddMinutes(WindowMinutes[^1]);

        for (var i = 0; i < future.Count; i++)
        {
            var p = future[i];
            if (p.Time <= entryTime || p.Time > horizon)
                continue;

            var move = Move(isCall, entryPrice, p.Ltp);
            var target = move >= barrierPoints;
            var stop = move <= -barrierPoints;

            if (!target && !stop)
                continue;

            if (target && stop)
            {
                // Unreachable for a single price unless barrierPoints <= 0. Flagged, never guessed.
                anomaly = $"Both TARGET and SL detected at same timestamp {p.Time:O} (move={move})";
                break;
            }

            // Same-timestamp contradiction across duplicate rows: two future records sharing this
            // exact instant disagree on which barrier was hit. Do not pick one.
            var conflicting = false;
            for (var j = i + 1; j < future.Count && future[j].Time == p.Time; j++)
            {
                var otherMove = Move(isCall, entryPrice, future[j].Ltp);
                if ((target && otherMove <= -barrierPoints) || (stop && otherMove >= barrierPoints))
                {
                    conflicting = true;
                    break;
                }
            }

            if (conflicting)
            {
                anomaly = $"Both TARGET and SL detected at same timestamp {p.Time:O}";
                break;
            }

            firstHitTime = p.Time;
            firstHitResult = target ? OutcomeResult.Target : OutcomeResult.StopLoss;
            break;
        }

        var windows = new WindowOutcome[WindowMinutes.Length];
        var completed = 0;

        for (var w = 0; w < WindowMinutes.Length; w++)
        {
            var minutes = WindowMinutes[w];
            var windowEnd = entryTime.AddMinutes(minutes);

            // Not finished elapsing — leave every field null rather than classifying it NONE.
            if (asOf < windowEnd)
            {
                windows[w] = WindowOutcome.NotDue;
                continue;
            }

            // An anomaly means the path is not trustworthy; record no classification for it.
            if (anomaly != null)
            {
                windows[w] = WindowOutcome.NotDue;
                continue;
            }

            decimal? maxFavorable = null;
            decimal? maxAdverse = null;

            foreach (var p in future)
            {
                if (p.Time <= entryTime || p.Time > windowEnd)
                    continue;

                var move = Move(isCall, entryPrice, p.Ltp);
                if (maxFavorable == null || move > maxFavorable) maxFavorable = move;
                if (maxAdverse == null || move < maxAdverse) maxAdverse = move;
            }

            var hitInsideWindow = firstHitTime.HasValue && firstHitTime.Value <= windowEnd;
            var result = hitInsideWindow ? firstHitResult! : OutcomeResult.None;

            windows[w] = new WindowOutcome(
                Result: result,
                TargetHit: result == OutcomeResult.Target,
                SlHit: result == OutcomeResult.StopLoss,
                MaxFavorable: maxFavorable,
                MaxAdverse: maxAdverse);

            completed = minutes;
        }

        var secondsToFirstHit = firstHitTime.HasValue
            ? (int)Math.Round((firstHitTime.Value - entryTime).TotalSeconds)
            : (int?)null;

        return new OutcomeCalculation(
            windows[0], windows[1], windows[2], completed, anomaly, secondsToFirstHit);
    }

    /// <summary>
    /// Directional move in index points. Positive is favourable for the given direction, so CALL
    /// and PUT results are directly comparable.
    /// </summary>
    public static decimal Move(bool isCall, decimal entryPrice, decimal futureLtp) =>
        isCall ? futureLtp - entryPrice : entryPrice - futureLtp;

    /// <summary>
    /// TrendDirection -> traded direction. Returns null for anything that is not exactly Bullish
    /// or Bearish, including null/empty — the caller must skip and log rather than assume one.
    /// </summary>
    public static string? DirectionFromTrend(string? trendDirection)
    {
        if (string.Equals(trendDirection, "Bullish", StringComparison.OrdinalIgnoreCase))
            return TradingConst.SignalType.CALL;

        if (string.Equals(trendDirection, "Bearish", StringComparison.OrdinalIgnoreCase))
            return TradingConst.SignalType.PUT;

        return null;
    }
}
