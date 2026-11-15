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
/// approved it. The max-daily-loss check below still returns 0 lots unless EnableDailyLossLimit is 0 — that is
/// a risk control, not sizing.
/// </summary>
public class AdaptiveLotSizingService
{
    internal const string EnableDailyLossLimitKey = "EnableDailyLossLimit";
    internal const string AdaptiveLotMinKey = "AdaptiveLotMin";

    /// <summary>The pure daily-loss decision: a reason when the day's loss budget is used up, else null.</summary>
    internal static string? DailyLossReason(decimal capital, decimal maxLossPercent, decimal totalLoss, decimal netProfit, decimal unrealizedPnl)
    {
        var maxLossPerDay = capital * maxLossPercent / 100m;
        var combinedLoss = -(netProfit + unrealizedPnl);
        return totalLoss >= maxLossPerDay || combinedLoss >= maxLossPerDay
            ? $"Max daily loss reached (₹{maxLossPerDay:F0} budget) — no trade"
            : null;
    }

    public async Task<LotSizeDecision> DetermineAsync(SignalContext ctx, MarketSnapshot market, decimal confidence)
    {
        var s = ctx.Settings;
        var todayStart = IstClock.TodayStart;

        // ── Max daily loss reached → no trade ──────────────────────────────────
        // Same read/formula as CircuitBreakerGate - a deliberate defensive duplicate. CircuitBreakerGate is
        // currently not registered in SignalWorker, so THIS is the daily-loss limit that actually runs: on
        // 2026-10-01 it blocked 3,130 signal cycles from 09:52 ("Max daily loss reached (4000 budget)").
        // StrategySettings EnableDailyLossLimit (per symbol, default on) switches it off; 0 = no daily-loss stop.
        if (ctx.StrategySettings.GetBool(EnableDailyLossLimitKey, ctx.Symbol, true))
        {
            var pnl = await ctx.PnlRepo.FirstOrDefaultAsync(p => p.TradeDate >= todayStart);
            if (pnl != null)
            {
                var blocked = DailyLossReason(s.Capital, s.MaxLossPercent, pnl.TotalLoss, pnl.NetProfit, pnl.UnrealizedPnl);
                if (blocked != null)
                    return new LotSizeDecision(0, blocked);
            }
        }

        // ── Fixed lot size ──────────────────────────────────────────────────────
        // Always AdaptiveLotMin (default 1). The former confidence-band scaling (1/2/3 lots) and
        // its below-band 0-lot rejection are gone: quantity is no longer a function of confidence.
        // The old high-ATR and consecutive-loss reducers were size *reductions* only — at a fixed
        // single lot they could never reduce below AdaptiveLotMin, so they are removed rather than
        // left in as permanent no-ops. Loss-streak protection remains enforced by ConsecutiveLossGate
        // (hard stop at MaxConsecLosses) and CircuitBreakerGate, which are untouched.
        // StrategySettings AdaptiveLotMin (per symbol, e.g. BANKNIFTY = 2) overrides the appsettings value.
        var lots = Math.Max(1, ctx.StrategySettings.GetInt(AdaptiveLotMinKey, ctx.Symbol, s.AdaptiveLotMin));

        return new LotSizeDecision(lots, $"fixed {lots} lot(s)");
    }
}
