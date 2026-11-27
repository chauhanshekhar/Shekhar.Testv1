using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.Worker.Exits;

/// <summary>
/// Audit trail of the option premium ExitWorker acted on (see <see cref="SignalOptionTick"/>).
///
/// Strictly observational and fail-safe: it runs in its OWN DI scope/DbContext, so a failed insert
/// can never poison the exit cycle's change tracker, and every exception is swallowed after being
/// logged. Nothing here may delay or block an exit decision beyond one small INSERT.
/// </summary>
public static class SignalOptionTickRecorder
{
    /// <summary>
    /// One row per open signal, using the same price resolution the exit ladder uses
    /// (<see cref="ExitDataLoader.ResolveLtps"/>), so the stored value is the value acted on.
    /// Pure — no I/O — so the source classification is unit-testable.
    /// </summary>
    public static List<SignalOptionTick> BuildRows(
        IReadOnlyList<Signal> openSignals,
        Dictionary<string, List<OptionChainData>> chainBySymbol,
        Dictionary<string, decimal> liveOptionQuotes,
        Dictionary<string, decimal> ltpBySymbol,
        DateTime nowIst)
    {
        var rows = new List<SignalOptionTick>(openSignals.Count);

        foreach (var signal in openSignals)
        {
            if (string.IsNullOrWhiteSpace(signal.OptionSymbol))
                continue;

            var (optionLtp, _, _) = ExitDataLoader.ResolveLtps(signal, chainBySymbol, liveOptionQuotes);

            // Mirrors ResolveLtps: a live quote only wins when the symbol's chain is present, so classify
            // from the resolved value rather than from the quote dictionary alone.
            var fromQuote = optionLtp > 0
                && liveOptionQuotes.TryGetValue(signal.OptionSymbol, out var quote)
                && quote == optionLtp;

            rows.Add(new SignalOptionTick
            {
                SignalId = signal.Id,
                Symbol = signal.Symbol,
                OptionSymbol = signal.OptionSymbol,
                TickTime = nowIst,
                OptionLtp = optionLtp,
                Source = optionLtp <= 0
                    ? SignalOptionTickSource.None
                    : fromQuote ? SignalOptionTickSource.Quote : SignalOptionTickSource.Chain,
                UnderlyingLtp = ltpBySymbol.TryGetValue(signal.Symbol, out var underlying) ? underlying : 0m,
                OptionSL = signal.OptionSL,
                ProtectedSL = signal.ProtectedSL,
                Status = signal.Status ?? "",
            });
        }

        return rows;
    }

    /// <summary>Persists the rows in one INSERT batch in a separate scope. Never throws.</summary>
    public static async Task RecordAsync(
        IServiceScopeFactory scopeFactory,
        List<SignalOptionTick> rows,
        ILogger logger)
    {
        if (rows.Count == 0)
            return;

        // After a failure (typically the migration not applied yet) stop retrying for a while, so a
        // missing table costs one warning a minute instead of one per second.
        if (DateTime.UtcNow < _retryAfterUtc)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<SignalOptionTick>>();
            await repo.AddRangeAsync(rows);
        }
        catch (Exception ex)
        {
            _retryAfterUtc = DateTime.UtcNow + FailureBackoff;

            // Audit data only — a lost row must never affect trading.
            logger.LogWarning(ex,
                "SignalOptionTick write failed for {Count} row(s); retrying in {Seconds}s, exits are unaffected",
                rows.Count, (int)FailureBackoff.TotalSeconds);
        }
    }

    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(60);
    private static DateTime _retryAfterUtc = DateTime.MinValue;
}
