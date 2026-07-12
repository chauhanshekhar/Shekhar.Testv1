namespace Trading.Worker.SignalGates;

public class KillSwitchGate : IPreSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var enabled = ctx.RuntimeSettings.IsAutoTradingEnabled();
        return Task.FromResult(enabled ? null : $"Kill switch active — AutoTradingEnabled=false ({ctx.Symbol})");
    }
}
