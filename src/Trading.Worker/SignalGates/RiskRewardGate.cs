using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

public class RiskRewardGate : IPostSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (signal.OptionSL <= 0 || signal.OptionT2 <= 0)
            return Task.FromResult<string?>(null);

        // Use entry trigger value so risk/reward are on the same scale as OptionSL/T2.
        var entry = PositionTrigger.EntryValue(signal);
        if (entry <= 0)
            return Task.FromResult<string?>(null);

        var risk   = entry - signal.OptionSL;
        var reward = signal.OptionT2 - entry;

        return Task.FromResult(risk > 0 && reward < risk * 2m
            ? (string?)$"R:R {reward:F1}:{risk:F1} below 2:1 — signal suppressed ({ctx.Symbol})"
            : null);
    }
}
