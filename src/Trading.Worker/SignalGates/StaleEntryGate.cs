using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks an entry when the LIVE index has already moved against the signal by a fraction of its
/// risk. Signal.EntryPrice and StopLoss are anchored to the last closed 1m candle close
/// (MarketSnapshot), up to ~1-2 minutes old, while ExitWorker checks stops against live ticks -
/// so a trade can start most of the way to its stop. 2026-10-01 signal 115: live BANKNIFTY was
/// already at the index SL when the signal fired; INDEX_SL_HIT 4 s later.
///
/// Off by default (EnableStaleEntryGate). HeroZero is exempt, like IndexStopFloorGate: its exits
/// are premium percentages, not the index stop.
///
/// HOLD UNTIL NEXT CANDLE (StaleEntryHoldUntilNextCandle, default on, 0 = off): once a setup is blocked, it stays blocked while its
/// entry price (the last closed 1m candle close) is unchanged. SignalWorker re-runs every second, so without the hold a setup blocked
/// on one tick fires on the next favourable tick of the same candle: 2026-10-05 signal 124 (-4,038), 2026-10-06 signals 137 (-1,312)
/// and 138 (+192) all fired 1 s after block cycles. A fresh candle (new entry price) is evaluated from scratch.
///
/// Every evaluation (pass or block) writes one structured log line with the tick price/time/age and the adverse
/// move in points and R, so near misses are measurable: on 2026-10-05 signal 124 passed at 0.24R on the tick this gate
/// could read, while the next tick, 5 s later in the same minute, put it at 0.30R.
/// </summary>
public class StaleEntryGate : IPostSignalGate
{
    internal const string EnableKey = "EnableStaleEntryGate";
    internal const string MaxAdverseKey = "StaleEntryMaxAdverseR";
    internal const decimal DefaultMaxAdverseR = 0.25m;
    internal const string HoldKey = "StaleEntryHoldUntilNextCandle";

    /// <summary>A hold never outlives this, even if the entry price somehow stays identical across candles.</summary>
    internal static readonly TimeSpan MaxHold = TimeSpan.FromMinutes(2);

    // One gate instance serves every symbol (SignalWorker.PostGates is static), so the memory is keyed per symbol and side.
    private static readonly ConcurrentDictionary<string, (decimal Entry, DateTime BlockedAt)> Held = new();

    internal static void ClearHeld() => Held.Clear();

    /// <summary>Test seam: records a block exactly as BlockAsync does.</summary>
    internal static void SeedHeld(string symbol, string signalType, decimal entry, DateTime blockedAt)
        => Held[$"{symbol}|{signalType}"] = (entry, blockedAt);

    /// <summary>True while the setup blocked at <paramref name="heldEntry"/> is still the same candle's setup.</summary>
    internal static bool IsStillHeld(decimal heldEntry, DateTime heldAt, decimal entry, DateTime now)
        => entry == heldEntry && now - heldAt < MaxHold && now >= heldAt;

    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return null;

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return null;

        var log = ctx.ServiceProvider?.GetService<ILoggerFactory>()?.CreateLogger<StaleEntryGate>();
        var holdOn = ctx.StrategySettings.GetBool(HoldKey, ctx.Symbol, true);
        var holdKey = $"{ctx.Symbol}|{signal.SignalType}";

        if (holdOn && Held.TryGetValue(holdKey, out var held))
        {
            if (IsStillHeld(held.Entry, held.BlockedAt, signal.EntryPrice, IstClock.Now))
            {
                log?.LogInformation("StaleEntryGate {Symbol} {SignalType}: entry={Entry} decision=HOLD (blocked at {BlockedAt:HH:mm:ss}, same candle)",
                    ctx.Symbol, signal.SignalType, signal.EntryPrice, held.BlockedAt);
                return $"Stale entry held until the next 1m candle (blocked {held.BlockedAt:HH:mm:ss}, candle entry {signal.EntryPrice:F2}) ({ctx.Symbol})";
            }

            Held.TryRemove(holdKey, out _);
        }

        // Latest tick, no time filter: StaleDataGate (a pre gate) has already blocked a stale feed.
        var latest = (await ctx.TickRepo.FindAsync(
                t => t.Symbol == ctx.Symbol,
                q => q.OrderByDescending(t => t.TickTime).Take(1)))
            .FirstOrDefault();

        if (latest is null || latest.LastPrice <= 0)
        {
            log?.LogWarning("StaleEntryGate {Symbol} {SignalType}: no usable tick, gate skipped (entry={Entry})",
                ctx.Symbol, signal.SignalType, signal.EntryPrice);
            return null;
        }

        var maxAdverseR = ctx.StrategySettings.GetDecimal(MaxAdverseKey, ctx.Symbol, DefaultMaxAdverseR);
        var reason = Evaluate(signal.SignalType, signal.EntryPrice, signal.StopLoss, latest.LastPrice, maxAdverseR);
        var measured = Measure(signal.SignalType, signal.EntryPrice, signal.StopLoss, latest.LastPrice);
        var ageSeconds = (IstClock.Now - latest.TickTime).TotalSeconds;

        log?.LogInformation(
            "StaleEntryGate {Symbol} {SignalType}: entry={Entry} stop={Stop} riskPts={RiskPts} tick={Tick} tickTime={TickTime:HH:mm:ss.fff} " +
            "tickAgeSec={TickAgeSec:F1} adversePts={AdversePts} adverseR={AdverseR} limitR={LimitR} decision={Decision}",
            ctx.Symbol, signal.SignalType, signal.EntryPrice, signal.StopLoss, measured?.RiskPoints, latest.LastPrice, latest.TickTime,
            ageSeconds, measured?.AdversePoints, measured?.AdverseR, maxAdverseR, reason is null ? "PASS" : "BLOCK");

        if (reason is not null && holdOn)
            Held[holdKey] = (signal.EntryPrice, IstClock.Now);

        return reason is null
            ? null
            : $"{reason} [tick {latest.TickTime:HH:mm:ss}, {ageSeconds:F0}s old] ({ctx.Symbol})";
    }

    /// <summary>The adverse move behind the decision: points against the trade and as a fraction of the risk. Null when undefined.</summary>
    internal static (decimal RiskPoints, decimal AdversePoints, decimal AdverseR)? Measure(
        string signalType, decimal entry, decimal stop, decimal live)
    {
        var risk = Math.Abs(entry - stop);
        if (risk <= 0 || live <= 0)
            return null;

        var adverse = signalType == TradingConst.SignalType.PUT ? live - entry : entry - live;
        return (risk, adverse, adverse / risk);
    }

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(string signalType, decimal entry, decimal stop, decimal live, decimal maxAdverseR)
    {
        if (maxAdverseR <= 0 || Measure(signalType, entry, stop, live) is not { } m)
            return null;

        return m.AdverseR >= maxAdverseR
            ? $"Stale entry: live {live:F2} is {m.AdversePoints:F1} pts ({m.AdverseR:F2}R) against candle entry " +
              $"{entry:F2}, stop {stop:F2}; max {maxAdverseR:F2}R"
            : null;
    }
}
