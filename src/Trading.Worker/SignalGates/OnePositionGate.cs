using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// One open position at a time, across all symbols.
///
/// Keys off the signal's actual STATUS. It previously inferred "open" from the absence of a
/// TradeJournal row, which meant any signal that could never produce one — ORDER_REJECTED,
/// ORDER_UNKNOWN, CLOSE_FAILED — suppressed every new signal on every symbol for the rest of the
/// day. A single failed placement halted all trading until midnight, and only a manual journal
/// insert released it (2026-08-24: signal 18 blocked SENSEX, BANKNIFTY and NIFTY simultaneously).
///
/// Scope is deliberately GLOBAL, not per-symbol: this is a one-position-at-a-time capital rule,
/// and narrowing it to per-symbol would let three positions run at once. The message names the
/// symbol being blocked, not the scope of the check.
/// </summary>
public class OnePositionGate : IPreSignalGate
{
    /// <summary>
    /// Statuses that represent real exposure. ExitPending counts: the exit order is in flight but
    /// the position is not closed until the broker confirms it.
    ///
    /// Everything absent from this set is terminal or never opened — CLOSED, ORDER_REJECTED,
    /// ORDER_UNKNOWN, CLOSE_FAILED, MANUAL_CHECK_REQUIRED, PENDING. PENDING is excluded because a
    /// signal only sits there while placement is in flight; BrokerOrderSyncWorker moves it on, and
    /// treating it as an open position would block on every in-flight order.
    /// </summary>
    private static readonly string[] OpenStatuses =
    [
        SignalStatus.OrderPlaced,
        SignalStatus.Active,
        SignalStatus.PartialExitPending,
        SignalStatus.ExitPending,
    ];

    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var todayStart = TradingConstants.TodayUtcStart();

        var openSignals = await ctx.SignalRepo.FindAsync(
            s => s.CreatedOn >= todayStart && OpenStatuses.Contains(s.Status));

        if (openSignals.Count == 0)
            return null;

        var first = openSignals[0];
        return $"Open position exists — new signal suppressed ({ctx.Symbol}); " +
               $"holding SignalId={first.Id} {first.Symbol} {first.SignalType} status={first.Status}";
    }

    /// <summary>Whether a status represents real exposure. Exposed for testing.</summary>
    internal static bool IsOpenStatus(string status) => OpenStatuses.Contains(status);
}
