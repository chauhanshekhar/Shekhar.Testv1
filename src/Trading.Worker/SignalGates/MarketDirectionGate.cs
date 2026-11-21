using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Market-wide direction confirmation. Every active index votes +1 (5m EMA20 &gt; EMA50) or -1 (5m EMA20 &lt; EMA50);
/// a CALL needs the score to be at least +MarketDirectionMinScore and a PUT at most -MarketDirectionMinScore.
/// With all three indices active the default of 2 means all three agree, or two agree and the third is flat (a 2-1 split is
/// only +/-1, "weak", and is blocked). MarketDirectionMinScore = 1 is the looser "simple majority of 3" setting.
///
/// EXPERIMENT, OFF BY DEFAULT. There is no historical evidence behind it yet. Two existing findings argue
/// against assuming it helps: entries aligned with the 5m trend ("mature" entries) went wrong more often than early ones,
/// and the three indices are strongly correlated, so three votes are not three independent confirmations. The related
/// CrossIndexAgreementGate (which has research behind it) is a separate, also-off gate. Run this on paper and measure the
/// blocked vs passed outcomes before relying on it. It is a loss avoider at best, never an entry signal.
///
/// Post-signal gate: the direction only exists once the signal is built. It only ever blocks; it never adds a trade.
///   EnableMarketDirectionGate              0/1  (default off; per symbol or global)
///   MarketDirectionMinScore                1..3 (default 2) votes of net agreement required
///   RequireIndividualTimeframeAlignment    0/1  (default off) the signal's own index must also have its 1m EMA20/EMA50
///                                               in the trade direction (a 1m/5m conflict, or a flat 1m, blocks)
///   BlockOnNeutralMarket                   0/1  (default on) an index whose 5m EMA20 equals EMA50 blocks, like the
///                                               "insufficient direction data" case of the proposal
///   MarketDataMaxAgeSeconds                int  (default 600) the newest CLOSED 5m bar of an index may have ended at most
///                                               this long ago. 60 s (the proposal's figure) is shorter than a 5m bar
///                                               and would drop every vote, hence the larger default.
/// Missing or stale data for an OTHER index removes its vote (it is not counted as a bearish or bullish vote). If fewer than
/// two indices can vote, or the signal's own 5m EMAs are missing, the gate lets the signal through, like the other entry gates.
/// HeroZero is exempt.
/// </summary>
public class MarketDirectionGate : IPostSignalGate
{
    internal const string EnableKey = "EnableMarketDirectionGate";
    internal const string MinScoreKey = "MarketDirectionMinScore";
    internal const string RequireAlignmentKey = "RequireIndividualTimeframeAlignment";
    internal const string BlockOnNeutralKey = "BlockOnNeutralMarket";
    internal const string MaxAgeKey = "MarketDataMaxAgeSeconds";
    internal const int DefaultMinScore = 2, DefaultMaxAgeSeconds = 600;
    private const int MinBars5m = 20;     // same minimum MarketSnapshotBuilder needs before it trusts a 5m EMA

    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return null;

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return null;

        var minScore = Math.Clamp(ctx.StrategySettings.GetInt(MinScoreKey, ctx.Symbol, DefaultMinScore), 1, 3);
        var requireAlignment = ctx.StrategySettings.GetBool(RequireAlignmentKey, ctx.Symbol, false);
        var blockOnNeutral = ctx.StrategySettings.GetBool(BlockOnNeutralKey, ctx.Symbol, true);
        var maxAge = Math.Max(60, ctx.StrategySettings.GetInt(MaxAgeKey, ctx.Symbol, DefaultMaxAgeSeconds));
        var now = DateTime.UtcNow;

        var others = ctx.Instruments.GetActive()
            .Select(i => i.Symbol)
            .Where(s => !s.Equals(ctx.Symbol, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var votes = new List<(string Symbol, int? Direction)>(others.Count);
        foreach (var other in others)
        {
            var sym5m = $"{other}_FUT_5m";
            var recent = await ctx.CandleRepo.FindAsync(
                c => c.Symbol == sym5m,
                q => q.OrderByDescending(c => c.CandleTime).Take(120));
            votes.Add((other, FiveMinuteDirection(recent, now, maxAge, (closes, p) => ctx.Ema.GetEma(closes, p))));
        }

        var own5m = Direction(market.Ema20_5m, market.Ema50_5m);
        var own1m = Direction(market.Ema20, market.Ema50);

        var reason = Evaluate(signal.SignalType, ctx.Symbol, own5m, own1m, votes, minScore, requireAlignment, blockOnNeutral);
        return reason;
    }

    /// <summary>+1 bullish, -1 bearish, 0 flat, null when either EMA is missing (0 means "not enough candles").</summary>
    internal static int? Direction(decimal ema20, decimal ema50)
    {
        if (ema20 <= 0m || ema50 <= 0m) return null;
        return Math.Sign(ema20 - ema50);
    }

    /// <summary>
    /// 5m EMA20/EMA50 direction of another index from its CLOSED 5m bars (the forming bar is excluded, as in
    /// MarketSnapshotBuilder). Null when there are too few bars or the newest closed bar is older than <paramref name="maxAgeSeconds"/>.
    /// </summary>
    internal static int? FiveMinuteDirection(IReadOnlyCollection<Candle> candles, DateTime nowUtc, int maxAgeSeconds,
        Func<List<decimal>, int, decimal> ema)
    {
        var closed = candles
            .Where(c => MarketSnapshotBuilder.IsClosedBar(c.CandleTime, 5, nowUtc))
            .OrderBy(c => c.CandleTime)
            .TakeLast(100)
            .ToList();

        if (closed.Count < MinBars5m)
            return null;

        // A feed that stopped leaves old bars in the table: stale data counts as no vote, not as the last direction it had.
        if (closed[^1].CandleTime.AddMinutes(5) < nowUtc.AddSeconds(-maxAgeSeconds))
            return null;

        var closes = closed.Select(c => c.Close).ToList();
        return Direction(ema(closes, 20), ema(closes, 50));
    }

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing. Null = proceed.</summary>
    internal static string? Evaluate(string signalType, string symbol, int? own5m, int? own1m,
        IReadOnlyList<(string Symbol, int? Direction)> others, int minScore, bool requireAlignment, bool blockOnNeutral)
    {
        // The signal's own 5m trend is the anchor; without it there is nothing to confirm, so do not guess.
        if (own5m is null)
            return null;

        var isCall = signalType == TradingConst.SignalType.CALL;
        var wanted = isCall ? 1 : -1;
        var side = isCall ? "CALL" : "PUT";

        if (requireAlignment && own1m is { } m1 && (m1 != wanted || own5m.Value != wanted))
            return $"Market direction gate: {symbol} 1m EMA {Label(m1)} / 5m EMA {Label(own5m.Value)} - {side} needs both timeframes {Label(wanted)}";

        var votes = new List<(string Symbol, int Direction)> { (symbol, own5m.Value) };
        votes.AddRange(others.Where(o => o.Direction.HasValue).Select(o => (o.Symbol, o.Direction!.Value)));

        if (votes.Count < 2)
            return null;

        if (blockOnNeutral && votes.Any(v => v.Direction == 0))
            return $"Market direction gate: {string.Join(", ", votes.Where(v => v.Direction == 0).Select(v => v.Symbol))} 5m EMA20 = EMA50 (neutral) - {side} not confirmed";

        var score = votes.Sum(v => v.Direction);
        var signed = isCall ? score : -score;
        var required = Math.Min(minScore, votes.Count);

        if (signed >= required)
            return null;

        var detail = string.Join(", ", votes.Select(v => $"{v.Symbol} {Label(v.Direction)}"));
        return $"Market direction gate: 5m score {score:+0;-0;0} over {votes.Count} indices (need {(isCall ? "+" : "-")}{required}; {detail}) - {side} not confirmed";
    }

    private static string Label(int direction) => direction > 0 ? "bullish" : direction < 0 ? "bearish" : "flat";
}
