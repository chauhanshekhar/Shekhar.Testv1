using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

/// <summary>
/// Diagnostic wrapper around SignalEngineService's signal-generation outcome — carries every
/// failed condition when Signal is null instead of losing that information at a bare `return null`.
/// Purely additive: GenerateSignal(...) still returns the same Signal?/null it always did.
/// </summary>
public sealed class SignalGenerationResult
{
    public Signal? Signal { get; }
    public IReadOnlyList<string> FailureReasons { get; }

    // Populated only when EnableSignalScoring is on for the symbol — the winning (or best-attempted,
    // if blocked) candidate's score breakdown, carried through to SignalWorker for the audit record.
    // Null in legacy (non-scoring) mode.
    public SignalScore? Score { get; }

    private SignalGenerationResult(Signal? signal, IReadOnlyList<string> failureReasons, SignalScore? score)
    {
        Signal = signal;
        FailureReasons = failureReasons;
        Score = score;
    }

    public static SignalGenerationResult Success(Signal signal, SignalScore? score = null) =>
        new(signal, Array.Empty<string>(), score);

    public static SignalGenerationResult Failed(params string[] reasons) => new(null, reasons, null);

    public static SignalGenerationResult Failed(IReadOnlyList<string> reasons, SignalScore? score = null) =>
        new(null, reasons, score);
}
