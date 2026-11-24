using Trading.Worker;
using Trading.Worker.SignalGates;

namespace Trading.Backtest.Entry;

/// <summary>
/// Which post-signal gates the live SignalWorker actually runs. The replay reads the same StrategySettings rows as live, but a row
/// can be switched on for a gate that is commented out of <c>SignalWorker.PostGates</c>; live then ignores it, so the replay must too.
/// </summary>
internal static class LiveGates
{
    private static readonly HashSet<Type> Registered = SignalWorker.PostGates.Select(g => g.GetType()).ToHashSet();

    /// <summary>True when <typeparamref name="T"/> is in the live <c>SignalWorker.PostGates</c> array.</summary>
    public static bool Has<T>() where T : IPostSignalGate => Registered.Contains(typeof(T));

    public static IReadOnlyCollection<string> Names => Registered.Select(t => t.Name).OrderBy(n => n).ToList();
}
