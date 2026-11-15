using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Only enter when the OTHER indices have been moving in the trade's direction: a CALL needs every other index up
/// over the lookback, a PUT needs every other index down. A move on one index alone is more often noise or a whipsaw.
///
/// Evidence (docs/research/2026-10-26-cross-index-agreement/, 263 closed signals 2026-08-04..10-26, index-candle replay
/// of the signal's own stop and Target 1, closed bars only): with both other indices agreeing over the prior 15 minutes
/// the stop rate was 35% vs 44% and the average result +0.27R vs -0.12R; costed at Rs20/order + 1pt/side the agreeing
/// group was +55k (PF 1.34) and the blocked group -36k (PF 0.69). The ordering held in both halves, in all three symbols
/// and for CALL and PUT, but the first half of the agreeing group was only break-even after costs, about 15 feature cells
/// were looked at (permutation p 0.003, roughly 0.05 after allowing for that), and the sample is 28 days. It is a loss
/// avoider, not a proven entry edge: it is OFF by default; enable it on paper first and re-run the research script on
/// the new signals before relying on it.
///
/// Post-signal gate: the direction only exists once the signal is built.
///   EnableCrossIndexAgreementGate  0/1                                        (default off)
///   CrossIndexLookbackMinutes      minutes of closed 1m bars to compare        (default 15)
///   CrossIndexMinAgree             how many of the other indices must agree    (default 2 = all of them)
///   CrossIndexAgreementSide        0 = CALL and PUT, 1 = CALL only, 2 = PUT only   (default 0)
/// The move of an index is last closed 1m close minus the close one lookback earlier; zero is not agreement. If an
/// index has too few closed bars (feed gap) it is not counted, and if none of the others has data the gate lets the
/// signal through, like the other entry gates. HeroZero is exempt.
/// </summary>
public class CrossIndexAgreementGate : IPostSignalGate
{
    internal const string EnableKey = "EnableCrossIndexAgreementGate";
    internal const string LookbackKey = "CrossIndexLookbackMinutes";
    internal const string MinAgreeKey = "CrossIndexMinAgree";
    internal const string SideKey = "CrossIndexAgreementSide";
    internal const int SideBoth = 0, SideCallOnly = 1, SidePutOnly = 2;
    internal const int DefaultLookback = 15, DefaultMinAgree = 2;
    internal const int MaxStaleMinutes = 3;   // newest closed bar of an index may be at most this old

    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return null;

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return null;

        var side = ctx.StrategySettings.GetInt(SideKey, ctx.Symbol, SideBoth);
        if (!AppliesTo(side, signal.SignalType))
            return null;

        var lookback = Math.Max(2, ctx.StrategySettings.GetInt(LookbackKey, ctx.Symbol, DefaultLookback));
        var minAgree = Math.Max(1, ctx.StrategySettings.GetInt(MinAgreeKey, ctx.Symbol, DefaultMinAgree));
        var now = DateTime.UtcNow;

        var others = ctx.Instruments.GetActive()
            .Select(i => i.Symbol)
            .Where(s => !s.Equals(ctx.Symbol, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var moves = new List<(string Symbol, decimal? Move)>(others.Count);
        foreach (var other in others)
        {
            var sym1m = $"{other}_1m";
            var recent = await ctx.CandleRepo.FindAsync(
                c => c.Symbol == sym1m,
                q => q.OrderByDescending(c => c.CandleTime).Take(lookback + 5));
            moves.Add((other, SignedMove(recent, lookback, now)));
        }

        var reason = Evaluate(signal.SignalType, moves, minAgree);
        return reason is null ? null : $"{reason} ({ctx.Symbol})";
    }

    /// <summary>Whether the gate applies to this direction under the configured side.</summary>
    internal static bool AppliesTo(int side, string signalType) => side switch
    {
        SideCallOnly => signalType == TradingConst.SignalType.CALL,
        SidePutOnly => signalType == TradingConst.SignalType.PUT,
        _ => true,
    };

    /// <summary>
    /// Close-to-close move over the last <paramref name="lookback"/> minutes using CLOSED 1m bars only (a bar is closed once
    /// its minute has elapsed, see MarketSnapshotBuilder.IsClosedBar). Null when fewer than half the bars exist.
    /// </summary>
    internal static decimal? SignedMove(IReadOnlyCollection<Candle> candles, int lookback, DateTime nowUtc)
    {
        var closed = candles
            .Where(c => MarketSnapshotBuilder.IsClosedBar(c.CandleTime, 1, nowUtc))
            .OrderBy(c => c.CandleTime)
            .TakeLast(lookback)
            .ToList();

        if (closed.Count < Math.Max(2, lookback / 2))
            return null;

        // A feed that stopped (symbol switched off, Groww gap) leaves old bars in the table; reading them would compare a move
        // from earlier in the day, or an earlier day, with a signal made now. Stale or non-contiguous data counts as no data.
        if (closed[^1].CandleTime < nowUtc.AddMinutes(-MaxStaleMinutes) ||
            closed[^1].CandleTime - closed[0].CandleTime > TimeSpan.FromMinutes(lookback + MaxStaleMinutes))
            return null;

        return closed[^1].Close - closed[0].Close;
    }

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(string signalType, IReadOnlyList<(string Symbol, decimal? Move)> moves, int minAgree)
    {
        var available = moves.Where(m => m.Move.HasValue).ToList();
        if (available.Count == 0)
            return null;

        var isCall = signalType == TradingConst.SignalType.CALL;
        var agreeing = available.Count(m => isCall ? m.Move > 0 : m.Move < 0);
        var required = Math.Min(minAgree, available.Count);

        if (agreeing >= required)
            return null;

        var detail = string.Join(", ", available.Select(m => $"{m.Symbol} {m.Move:+0.##;-0.##;0}"));
        return $"Cross-index gate: only {agreeing} of {available.Count} other indices moving {(isCall ? "up" : "down")} " +
               $"(need {required}; {detail}) - {signalType} needs the other indices to agree";
    }
}
