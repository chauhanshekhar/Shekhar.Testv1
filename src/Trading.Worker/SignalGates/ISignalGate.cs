namespace Trading.Worker.SignalGates;

public interface IPreSignalGate
{
    /// <summary>Returns null to proceed, or an abort-reason string.</summary>
    Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct);
}

public interface IPostSignalGate
{
    /// <summary>Returns null to proceed, or an abort-reason string.</summary>
    Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Domain.Entities.Signal signal, CancellationToken ct);
}
