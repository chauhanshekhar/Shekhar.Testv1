using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

public enum VolatilityState { Normal, Caution, SevereWhipsaw }

public sealed class VolatilityGateResult
{
    public VolatilityState State { get; init; }
    public int InstabilityScore { get; init; }
    public List<string> Reasons { get; init; } = new();
    public int EmaFlipCount1m { get; init; }
    public int CandleAlternationCount5m { get; init; }
    public int? Ema1mDirection { get; init; }
    public int? Ema5mDirection { get; init; }
}

/// <summary>
/// SHADOW ONLY whipsaw detector. It scores how unstable the signal's own index is from CLOSED candles and logs the result
/// next to every signal that reaches the post gates. It NEVER blocks (always returns null), so it cannot change trading.
/// The point is to collect forward evidence of what a "severe whipsaw" block would have removed (compare the
/// WHIPSAW_SHADOW log lines with the later trade outcome) before any blocking mode is considered.
///
/// Each index is scored independently; another index being volatile has no effect.
/// Signals implemented (thresholds are untested starting points, not validated values):
///   1. 1m EMA20/EMA50 direction changed &gt;= 3 times over the last 10 closed 1m bars
///   2. 5m candle direction (close vs open) alternated &gt;= 3 times over the last 5 closed 5m bars
///   3. 1m and 5m EMA directions disagree
/// NOT implemented, on purpose: VWAP-cross count (MarketSnapshot carries only the latest scalar VWAP, no per-bar series) and the
/// ATR% time-of-day percentile (no stored percentile table). They need data work first.
/// State: score 0-1 Normal, 2 Caution, 3 SevereWhipsaw.
///
/// Off by default.
///   EnableWhipsawShadow  0/1 (exact-symbol row, then global Symbol IS NULL row)
/// HeroZero is exempt. Exits (stop loss, target, trailing, square-off) are in other workers and are not touched.
/// </summary>
public class WhipsawShadowGate : IPostSignalGate
{
    internal const string EnableKey = "EnableWhipsawShadow";
    internal const int EmaFlipBars = 10, EmaFlipThreshold = 3;
    internal const int AltBars5m = 5, AltThreshold = 3;

    // One gate instance serves every symbol (SignalWorker.PostGates is static), so error throttling is keyed per symbol.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> LastErrorLogMs = new();



    public Task<string?> BlockAsync(
        SignalContext ctx,
        MarketSnapshot market,
        Signal signal,
        CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        try
        {
            var result = Evaluate(
                market.Candles1m,
                market.Candles5m,
                market.Ema20,
                market.Ema50,
                market.Ema20_5m,
                market.Ema50_5m);

            // Block CALL only during severe whipsaw.
            if (signal.SignalType == TradingConst.SignalType.CALL &&
                result.State == VolatilityState.SevereWhipsaw)
            {
                return Task.FromResult<string?>(
                    "CALL blocked: Severe whipsaw detected.");
            }
        }
        catch
        {
            // Fail open: evaluation error must not block trading.
        }

        return Task.FromResult<string?>(null);
    }


    /// <summary>Pure scoring from closed candles (chronological). Exposed for testing.</summary>
    internal static VolatilityGateResult Evaluate(IReadOnlyList<Candle> candles1m, IReadOnlyList<Candle> candles5m,
        decimal ema20, decimal ema50, decimal ema20_5m, decimal ema50_5m)
    {
        var reasons = new List<string>();

        var flips = CountEmaFlips(candles1m, EmaFlipBars);
        if (flips >= EmaFlipThreshold)
            reasons.Add($"1m EMA direction changed {flips} times in last {EmaFlipBars} bars");

        var alt = CountAlternations(candles5m, AltBars5m);
        if (alt >= AltThreshold)
            reasons.Add($"5m candles alternated direction {alt} times in last {AltBars5m} bars");

        var d1 = MarketDirectionGate.Direction(ema20, ema50);
        var d5 = MarketDirectionGate.Direction(ema20_5m, ema50_5m);
        if (d1 is { } a && d5 is { } b && a != 0 && b != 0 && a != b)
            reasons.Add("1m and 5m EMA directions disagree");

        var score = reasons.Count;
        return new VolatilityGateResult
        {
            InstabilityScore = score,
            Reasons = reasons,
            EmaFlipCount1m = flips,
            CandleAlternationCount5m = alt,
            Ema1mDirection = d1,
            Ema5mDirection = d5,
            State = StateFor(score),
        };
    }

    internal static VolatilityState StateFor(int score) =>
        score >= 3 ? VolatilityState.SevereWhipsaw : score == 2 ? VolatilityState.Caution : VolatilityState.Normal;

    /// <summary>
    /// Sign changes of (EMA20 - EMA50) across the last <paramref name="bars"/> closed bars. The EMAs are recomputed over the whole
    /// list (plain recursive EMA, seeded with the first close; diagnostic only). Zero differences are skipped, not counted as a flip.
    /// Returns 0 when there are fewer than 50 candles, since the EMA50 is not warmed up.
    /// </summary>
    internal static int CountEmaFlips(IReadOnlyList<Candle> candles1m, int bars)
    {
        if (candles1m.Count < 50) return 0;

        decimal e20 = candles1m[0].Close, e50 = candles1m[0].Close;
        const decimal k20 = 2m / 21m, k50 = 2m / 51m;
        var signs = new List<int>(candles1m.Count);
        for (var i = 0; i < candles1m.Count; i++)
        {
            var c = candles1m[i].Close;
            e20 += (c - e20) * k20;
            e50 += (c - e50) * k50;
            signs.Add(Math.Sign(e20 - e50));
        }

        var flips = 0;
        var prev = 0;
        for (var i = Math.Max(0, signs.Count - bars - 1); i < signs.Count; i++)
        {
            if (signs[i] == 0) continue;
            if (prev != 0 && signs[i] != prev) flips++;
            prev = signs[i];
        }
        return flips;
    }

    /// <summary>Direction changes (close vs open; dojis skipped) among the last <paramref name="bars"/> closed candles.</summary>
    internal static int CountAlternations(IReadOnlyList<Candle> candles, int bars)
    {
        var flips = 0;
        var prev = 0;
        for (var i = Math.Max(0, candles.Count - bars); i < candles.Count; i++)
        {
            var s = Math.Sign(candles[i].Close - candles[i].Open);
            if (s == 0) continue;
            if (prev != 0 && s != prev) flips++;
            prev = s;
        }
        return flips;
    }
}
