namespace Trading.SignalEngine.Services.Models;

/// <summary>
/// One direction's verdict: whether it fired, and the labelled state of EVERY condition that
/// decided it.
///
/// Passed is DERIVED from Checks rather than stored alongside it, so a condition cannot be
/// enforced without appearing in the failure list, nor listed without being enforced. That
/// coupling is the whole point: the previous inline version reported "MarketState is
/// StrongTrend" as a CALL blocker while that condition was commented out of the expression,
/// and a live diagnosis was misread as a result.
/// </summary>
public readonly record struct DirectionVerdict((string Label, bool Passed)[] Checks)
{
    public bool Passed => Checks.All(c => c.Passed);

    public IEnumerable<string> FailedLabels(string side) =>
        Checks.Where(c => !c.Passed).Select(c => $"{side}: {c.Label}");

    public string FormatChecklist() =>
        string.Join(Environment.NewLine, Checks.Select(c => $"{(c.Passed ? "✓" : "✗")} {c.Label}"));
}
