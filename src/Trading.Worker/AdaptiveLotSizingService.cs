using Trading.Domain;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

public record LotSizeDecision(int Lots, string Reason);

/// <summary>
/// Central, single-point lot sizing. Read-only against ctx.PnlRepo/ctx.JournalRepo, mirroring
/// CircuitBreakerGate/ConsecutiveLossGate's own query shape for consistency; does not call into or
/// modify those gates. Called once from StrategyManager.SelectSignalAsync after the confidence
/// check, for every strategy uniformly.
///
/// Sizing is FIXED at AdaptiveLotMin (default 1) — confidence no longer influences quantity.
/// Previously confidence bands drove 1/2/3 lots and, critically, returned 0 lots (= no trade) below
/// ConfidenceLot1Min, which silently discarded every deliberately-low-confidence setup (e.g. the
/// SINGLE whitelist rules, which are all < 65 confidence by design) after the engine had already
/// approved it. The max-daily-loss check below still returns 0 lots — that is a risk control, not
/// sizing, and is intentionally preserved.
/// </summary>
public class AdaptiveLotSizingService
{
    public async Task<LotSizeDecision> DetermineAsync(SignalContext ctx, MarketSnapshot market, decimal confidence)
    {
        var s = ctx.Settings;
        var todayStart = IstClock.TodayStart;

        // ── Max daily loss reached → no trade ──────────────────────────────────
        // Same read/formula as CircuitBreakerGate — a deliberate defensive duplicate (that gate
        // already runs pre-signal and would normally have blocked this cycle already), not a new
        // independent circuit breaker.
        var maxLossPerDay = s.Capital * s.MaxLossPercent / 100m;
        var pnl = await ctx.PnlRepo.FirstOrDefaultAsync(p => p.TradeDate >= todayStart);
        if (pnl != null)
        {
            var combinedLoss = -(pnl.NetProfit + pnl.UnrealizedPnl);
            if (pnl.TotalLoss >= maxLossPerDay || combinedLoss >= maxLossPerDay)
                return new LotSizeDecision(0, $"Max daily loss reached (₹{maxLossPerDay:F0} budget) — no trade");
        }

        // ── Fixed lot size ──────────────────────────────────────────────────────
        // Always AdaptiveLotMin (default 1). The former confidence-band scaling (1/2/3 lots) and
        // its below-band 0-lot rejection are gone: quantity is no longer a function of confidence.
        // The old high-ATR and consecutive-loss reducers were size *reductions* only — at a fixed
        // single lot they could never reduce below AdaptiveLotMin, so they are removed rather than
        // left in as permanent no-ops. Loss-streak protection remains enforced by ConsecutiveLossGate
        // (hard stop at MaxConsecLosses) and CircuitBreakerGate, which are untouched.
        var lots = Math.Max(1, s.AdaptiveLotMin);

        return new LotSizeDecision(lots, $"fixed {lots} lot(s)");
    }
}
