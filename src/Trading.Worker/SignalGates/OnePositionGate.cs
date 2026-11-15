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
///
/// A second check reads live Orders, because a status can go terminal while the broker still
/// holds contracts (2026-09-30, signals 91-93) - see <see cref="FindUnconfirmedExposure"/>.
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
    internal static readonly string[] OpenStatuses =
    [
        SignalStatus.OrderPlaced,
        SignalStatus.Active,
        SignalStatus.PartialExitPending,
        SignalStatus.ExitPending,
    ];

    /// <summary>StrategySettings key (global). No row = 15 minutes; 0 turns the order check off.</summary>
    internal const string OrderHoldMinutesKey = "OnePositionOrderHoldMinutes";
    internal const int DefaultOrderHoldMinutes = 15;

    private const string PaperPrefix = "PAPER-";

    private static readonly string[] TerminalOrderStatuses =
    [
        OrderStatus.Complete,
        OrderStatus.Rejected,
        OrderStatus.Cancelled,
        OrderStatus.Failed,
    ];

    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var openSignals = await FindOpenSignalsAsync(ctx);

        if (openSignals.Count > 0)
            return OpenPositionReason(ctx.Symbol, openSignals[0]);

        return await CheckOrderExposureAsync(ctx, null);
    }

    internal static async Task<IReadOnlyList<Signal>> FindOpenSignalsAsync(SignalContext ctx)
    {
        var todayStart = TradingConstants.TodayUtcStart();

        return await ctx.SignalRepo.FindAsync(
            s => s.CreatedOn >= todayStart && OpenStatuses.Contains(s.Status));
    }

    internal static string OpenPositionReason(string symbol, Signal first) =>
        $"Open position exists — new signal suppressed ({symbol}); " +
        $"holding SignalId={first.Id} {first.Symbol} {first.SignalType} status={first.Status}";

    /// <param name="ignoreSignalIds">
    /// Signals whose exposure is ignored here. Null from this gate, so every signal's exposure counts.
    /// </param>
    internal static async Task<string?> CheckOrderExposureAsync(SignalContext ctx, IReadOnlySet<long>? ignoreSignalIds)
    {

        // Status alone is not proof of flat. On 2026-09-30 signal 91 left the open statuses at
        // 10:04:54 with its lot still held at the broker (every SELL rejected), and signal 92 did
        // the same at 10:05:24 while its BUY was still filling - so 92 and 93 fired on top of a
        // live position. Live Orders rows are the broker's record: contracts bought and not sold,
        // or an entry still in flight, are exposure whatever the status says. Bounded by a hold
        // window (from order PLACEMENT, which sync polling never bumps) so a position closed in
        // the Groww app - which leaves no SELL row - cannot halt trading all day (2026-08-24).
        var holdMinutes = ctx.StrategySettings.GetInt(OrderHoldMinutesKey, null, DefaultOrderHoldMinutes);
        if (holdMinutes <= 0)
            return null;

        var nowIst = IstClock.Now;
        var since = nowIst.AddMinutes(-holdMinutes);

        var recentIds = (await ctx.OrderRepo.FindAsync(
                o => o.SignalId != null && o.OrderedAt >= since && !o.BrokerOrderId.StartsWith(PaperPrefix)))
            .Select(o => o.SignalId!.Value)
            .Distinct()
            .ToList();

        if (recentIds.Count == 0)
            return null;

        var orders = await ctx.OrderRepo.FindAsync(
            o => o.SignalId != null && recentIds.Contains(o.SignalId.Value));

        var exposure = FindUnconfirmedExposure(orders, nowIst, holdMinutes, ignoreSignalIds);
        return exposure is null ? null : $"{exposure} — new signal suppressed ({ctx.Symbol})";
    }

    /// <summary>
    /// The pure decision behind the order check. Only live orders (not PAPER-) of signals with an
    /// order placed inside the hold window count. Returns the block reason, or null. Exposed for
    /// testing.
    /// </summary>
    internal static string? FindUnconfirmedExposure(
        IEnumerable<Order> orders, DateTime nowIst, int holdMinutes, IReadOnlySet<long>? ignoreSignalIds = null)
    {
        if (holdMinutes <= 0)
            return null;

        var since = nowIst.AddMinutes(-holdMinutes);

        var bySignal = orders
            .Where(o => o.SignalId.HasValue && !IsPaper(o)
                        && (ignoreSignalIds is null || !ignoreSignalIds.Contains(o.SignalId.Value)))
            .GroupBy(o => o.SignalId!.Value);

        foreach (var g in bySignal)
        {
            var lastPlaced = g.Max(o => o.OrderedAt);
            if (lastPlaced < since)
                continue;

            var inFlight = g.FirstOrDefault(o => IsEntryBuy(o)
                                                 && o.OrderedAt >= since
                                                 && !TerminalOrderStatuses.Contains(o.Status));
            if (inFlight is not null)
                return $"Entry order in flight — SignalId={g.Key} {inFlight.Symbol} order {inFlight.Id} " +
                       $"{inFlight.Status} since {inFlight.OrderedAt:HH:mm:ss}";

            var bought = g.Where(IsEntryBuy).Sum(o => o.FilledQty);
            var sold = g.Where(o => o.Side.Equals("SELL", StringComparison.OrdinalIgnoreCase)).Sum(o => o.FilledQty);

            if (bought > sold)
                return $"Unconfirmed exposure — SignalId={g.Key} {g.First().Symbol} bought {bought}, sold {sold} " +
                       $"with no confirmed SELL (last order {lastPlaced:HH:mm:ss}; holds up to {holdMinutes}m)";
        }

        return null;
    }

    private static bool IsPaper(Order o)
        => o.BrokerOrderId.StartsWith(PaperPrefix, StringComparison.Ordinal);

    private static bool IsEntryBuy(Order o)
        => o.OrderPurpose == OrderPurpose.Entry
           && o.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a status represents real exposure. Exposed for testing.</summary>
    internal static bool IsOpenStatus(string status) => OpenStatuses.Contains(status);
}
