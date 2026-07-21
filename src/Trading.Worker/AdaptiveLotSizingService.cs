using Trading.Domain;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

public record LotSizeDecision(int Lots, string Reason);

/// <summary>
/// Central, single-point adaptive lot sizing — confidence bands, then risk modifiers.
/// Read-only against ctx.PnlRepo/ctx.JournalRepo, mirroring CircuitBreakerGate/ConsecutiveLossGate's
/// own query shape for consistency; does not call into or modify those gates. Called once from
/// StrategyManager.SelectSignalAsync after the confidence check, for every strategy uniformly.
/// All thresholds come from TradingSettings (ctx.Settings) — no hardcoded values.
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

        // ── Base lots from confidence bands ─────────────────────────────────────
        int lots = confidence >= s.ConfidenceLot3Min ? 3
            : confidence >= s.ConfidenceLot2Min ? 2
            : confidence >= s.ConfidenceLot1Min ? 1
            : 0;

        if (lots == 0)
            return new LotSizeDecision(0, $"Confidence {confidence:F0} below minimum lot band ({s.ConfidenceLot1Min:F0})");

        var reason = $"confidence {confidence:F0} -> {lots} lot(s)";

        // ── High ATR → reduce one lot ───────────────────────────────────────────
        // AtrPct (not AtrRaw) — AdaptiveLotHighAtrThreshold is percentage-scaled, matching the
        // same unit convention as TradingSettings.MinAtrPercent (AtrGate/SignalEngineService).
        // Comparing a percentage-scale setting against raw ATR points would fire on nearly every
        // cycle since raw ATR is always well above a value like 0.10-0.30.
        var highAtrThreshold = s.AdaptiveLotHighAtrThreshold.GetValueOrDefault(ctx.Symbol, 0.10m);
        if (market.AtrPct >= highAtrThreshold && lots > s.AdaptiveLotMin)
        {
            lots--;
            reason += $"; high ATR% {market.AtrPct:F2} >= {highAtrThreshold:F2} -> reduced 1 lot";
        }

        // ── Two consecutive losses → minimum lot ────────────────────────────────
        // Same read shape as ConsecutiveLossGate — a lighter, earlier tier than that gate's
        // hard-stop-at-MaxConsecLosses (3); this triggers at a smaller count and only shrinks size.
        var recentLosses = (await ctx.JournalRepo.FindAsync(j => j.TradeDate >= todayStart))
            .Where(j => !j.Remarks.StartsWith("PARTIAL_"))
            .OrderByDescending(j => j.TradeDate)
            .Take(s.AdaptiveLotConsecutiveLossCount)
            .ToList();

        if (recentLosses.Count >= s.AdaptiveLotConsecutiveLossCount && recentLosses.All(j => j.ProfitLoss < 0))
        {
            lots = s.AdaptiveLotMin;
            reason += $"; {s.AdaptiveLotConsecutiveLossCount} consecutive losses -> minimum lot ({s.AdaptiveLotMin})";
        }

        return new LotSizeDecision(Math.Max(lots, s.AdaptiveLotMin), reason);
    }
}
