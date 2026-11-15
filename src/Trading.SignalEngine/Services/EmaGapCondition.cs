namespace Trading.SignalEngine.Services;

/// <summary>
/// Condition 2b, shared by CALL and PUT (both entry evaluators and both SignalEngineService
/// copies), so the four call sites cannot drift apart. |EMA20 - EMA50| in index points must lie
/// strictly between EmaGapMinPoints and EmaGapMaxPoints.
///
/// Off by default (EnableEmaGapFilter). Per-symbol capable: 10 points is a very different move on
/// NIFTY than on SENSEX. The bounds are independent - deactivate EmaGapMaxPoints (or leave it
/// unset) for "gap above minimum" only, deactivate EmaGapMinPoints for "gap below maximum" only.
/// </summary>
internal static class EmaGapCondition
{
    internal const decimal DefaultMinPoints = 10m;

    /// <summary>Null when the condition does not apply (switch off, or both bounds deactivated).</summary>
    internal static (string Description, bool Passed)? Evaluate(
        decimal ema20,
        decimal ema50,
        Func<string, bool, bool> enabled,
        Func<string, bool> active,
        Func<string, decimal, decimal> threshold)
    {
        if (!enabled("EnableEmaGapFilter", false))
            return null;

        var useMin = active("EmaGapMinPoints");
        var useMax = active("EmaGapMaxPoints");
        if (!useMin && !useMax)
            return null;

        var min = threshold("EmaGapMinPoints", DefaultMinPoints);
        var max = threshold("EmaGapMaxPoints", decimal.MaxValue);
        var gap = Math.Abs(ema20 - ema50);

        var passed = (!useMin || gap > min) && (!useMax || gap < max);
        var lower = useMin ? min.ToString("0.##") : "0";
        var upper = useMax && max != decimal.MaxValue ? max.ToString("0.##") : "inf";

        return ($"2b |EMA20-EMA50| in ({lower},{upper}) - was {gap:F2}", passed);
    }
}
