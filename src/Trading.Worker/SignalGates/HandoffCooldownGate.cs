using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks a new entry on ANY symbol (both directions) within N minutes of any exit; HandoffCooldownSameDirectionOnly = 1 limits it to the same direction. OnePositionGate holds one position across all
/// symbols, so a setup on another index waits and fires the second the open trade exits - a stale setup entered with no fresh
/// check. DuplicateSignalGate's cooldown only looks at the same symbol, so it never sees this handoff.
///
/// Evidence (paper, 2026-09-30 to 2026-10-07): 10 trades entered within 2 minutes of another symbol's same-direction exit; 1 won,
/// 9 lost, -10,006 in total (about 10 trades over 7 sessions: directional evidence only).
///
/// Off by default (EnableHandoffCooldownGate). HandoffCooldownMinutes (default 5). HeroZero is exempt, like the other entry gates.
/// </summary>
public class HandoffCooldownGate : IPostSignalGate
{
    internal const string EnableKey = "EnableHandoffCooldownGate";
    internal const string MinutesKey = "HandoffCooldownMinutes";
    internal const string SameDirectionOnlyKey = "HandoffCooldownSameDirectionOnly";
    internal const int DefaultMinutes = 5;

    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return null;

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return null;

        var minutes = ctx.StrategySettings.GetInt(MinutesKey, ctx.Symbol, DefaultMinutes);
        if (minutes <= 0)
            return null;

        var sameDirectionOnly = ctx.StrategySettings.GetBool(SameDirectionOnlyKey, ctx.Symbol, false);
        var type = signal.SignalType;
        var now = IstClock.Now;
        var since = now.AddMinutes(-minutes);

        var recent = (await ctx.SignalRepo.FindAsync(x =>
                (!sameDirectionOnly || x.SignalType == type) &&
                x.ExitTime != null && x.ExitTime >= since))
            .OrderByDescending(x => x.ExitTime)
            .FirstOrDefault();

        return Evaluate(signal.SignalType, recent?.Symbol, recent?.ExitTime, recent?.ExitReason, now, minutes);
    }

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(string signalType, string? exitSymbol, DateTime? exitTime, string? exitReason, DateTime now, int minutes)
    {
        if (minutes <= 0 || exitTime is null)
            return null;

        var age = now - exitTime.Value;
        if (age < TimeSpan.Zero || age >= TimeSpan.FromMinutes(minutes))
            return null;

        return $"Handoff cooldown: {exitSymbol} exited {age.TotalSeconds:F0}s ago ({exitReason}) - " +
               $"no new entry on any symbol for {minutes}m after an exit";
    }
}
