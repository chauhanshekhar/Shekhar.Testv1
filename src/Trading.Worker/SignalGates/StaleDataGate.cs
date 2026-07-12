namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks signals when market feed data is stale.
/// Prevents trading on outdated LTP or OI snapshots.
/// </summary>
public class StaleDataGate : IPreSignalGate
{
    private static readonly TimeSpan MaxTickAge        = TimeSpan.FromMinutes(2);

    // 2x the OptionChainWorker poll interval (30s) — catches a frozen/dead
    // Groww feed within ~2 missed cycles without false-tripping on normal jitter.
    private static readonly TimeSpan MaxOptionChainAge = TimeSpan.FromSeconds(60);

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        if (market.LastTickAge == TimeSpan.MaxValue)
            return Task.FromResult<string?>("Stale data: no market tick received in last 10 minutes — feed may be down");

        if (market.LastTickAge > MaxTickAge)
            return Task.FromResult<string?>($"Stale tick: last tick was {market.LastTickAge.TotalSeconds:F0}s ago (max {MaxTickAge.TotalSeconds:F0}s)");

        if (market.LastOptionChainAge == TimeSpan.MaxValue)
            return Task.FromResult<string?>("Stale data: no option chain snapshot found — OptionChainWorker may be down");

        if (market.LastOptionChainAge > MaxOptionChainAge)
            return Task.FromResult<string?>($"Stale option chain: last snapshot was {market.LastOptionChainAge.TotalSeconds:F0}s ago (max {MaxOptionChainAge.TotalSeconds:F0}s)");

        return Task.FromResult<string?>(null);
    }
}
