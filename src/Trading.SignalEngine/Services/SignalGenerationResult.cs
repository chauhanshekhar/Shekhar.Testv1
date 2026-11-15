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

    private SignalGenerationResult(Signal? signal, IReadOnlyList<string> failureReasons)
    {
        Signal = signal;
        FailureReasons = failureReasons;
    }

    public static SignalGenerationResult Success(Signal signal) => new(signal, Array.Empty<string>());

    public static SignalGenerationResult Failed(params string[] reasons) => new(null, reasons);

    public static SignalGenerationResult Failed(IReadOnlyList<string> reasons) => new(null, reasons);
}
