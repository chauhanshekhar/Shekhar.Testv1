using Trading.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain.Entities;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Brokers.Groww;

// Strategy: paper trading mode — all orders are saved to the PaperTrades table.
// No broker API is called.
public class PaperOrderStrategy : IOrderStrategy
{
    private readonly IDbContextFactory<TradingDbContext> _dbFactory;
    private readonly IGrowwLiveDataService? _liveData;
    private readonly ILogger<PaperOrderStrategy>? _logger;

    public PaperOrderStrategy(
        IDbContextFactory<TradingDbContext> dbFactory,
        IGrowwLiveDataService? liveData = null,
        ILogger<PaperOrderStrategy>? logger = null)
    {
        _dbFactory = dbFactory;
        _liveData = liveData;
        _logger = logger;
    }

    public async Task<string> PlaceOrderAsync(Signal signal)
    {
        // Fill at the live premium when the order is placed, like the MARKET order a live entry
        // sends. signal.OptionLtp is the last chain snapshot (refreshed ~once a minute): on 27
        // paper entries 2026-09-30..10-05 it was 42 s old on average and off by 7.3 premium points
        // (Rs 781/trade), overstating paper P&L. ActivateSignalAsync re-anchors OptionSL/T1/T2 and
        // the journal entry price to this fill, exactly as it does for a live fill.
        var liveQuote = await FetchLiveQuoteAsync(_liveData, signal, _logger);

        var trade = new PaperTrade
        {
            SignalId         = signal.Id > 0 ? signal.Id : null,
            Symbol           = signal.Symbol,
            OptionSymbol     = signal.OptionSymbol,
            Side             = signal.SignalType,
            StrategyType     = signal.StrategyType,
            Quantity         = signal.Quantity > 0 ? signal.Quantity : 1,
            EntryPrice       = PaperEntryPrice(liveQuote, signal.OptionLtp, signal.EntryPrice),
            StopLoss         = signal.OptionSL > 0 ? signal.OptionSL : signal.StopLoss,
            Target1          = signal.OptionT1 > 0 ? signal.OptionT1 : signal.Target1,
            Target2          = signal.OptionT2 > 0 ? signal.OptionT2 : signal.Target2,
            // Leg2 — spread second leg
            Leg2OptionSymbol = signal.Leg2Symbol,
            Leg2EntryPrice   = signal.Leg2Ltp,
            Status           = TradingConst.TradeStatus.Open,
            CreatedOn        = IstClock.Now,
        };

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.PaperTrades.Add(trade);
        await db.SaveChangesAsync();

        return $"PAPER-{trade.Id}";
    }

    /// <summary>
    /// Live premium for the signal's option, or null: no service, no symbol, no usable quote, or
    /// the call failed (logged, never thrown - the chain price is then used).
    /// </summary>
    internal static async Task<decimal?> FetchLiveQuoteAsync(IGrowwLiveDataService? liveData, Signal signal, ILogger? logger)
    {
        if (liveData is null || string.IsNullOrWhiteSpace(signal.OptionSymbol))
            return null;

        try
        {
            var quote = await liveData.GetQuoteAsync(signal.Exchange, GrowwSegment.Fno, signal.OptionSymbol);
            return quote?.Ltp is > 0 ? quote.Ltp : null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "Paper entry: live quote failed for {OptionSymbol}; filling at the chain price {ChainLtp}",
                signal.OptionSymbol, signal.OptionLtp);
            return null;
        }
    }

    /// <summary>Paper entry fill: live quote, else the signal's chain premium, else the index entry.</summary>
    internal static decimal PaperEntryPrice(decimal? liveQuote, decimal signalOptionLtp, decimal signalEntryPrice)
        => liveQuote is > 0 ? liveQuote.Value
         : signalOptionLtp > 0 ? signalOptionLtp
         : signalEntryPrice;

    public async Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null, string? orderReferenceId = null)
    {
        var symbol = !string.IsNullOrEmpty(signal.OptionSymbol)
                        ? signal.OptionSymbol
                        : signal.Symbol;

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Fill price, best first: the premium the exit decision was made on (stamped on the Order
        // row as Price), else the latest option-chain snapshot, else entry. The chain alone is up
        // to ~30 s stale and misbooked partial sales on 2026-09-30.
        decimal? decisionPrice = null;
        if (!string.IsNullOrEmpty(orderReferenceId))
        {
            decisionPrice = await db.Orders
                .Where(o => o.OrderReferenceId == orderReferenceId)
                .Select(o => (decimal?)o.Price)
                .FirstOrDefaultAsync();
        }

        var latestLtp = await db.OptionChainData
            .Where(o => o.TradingSymbol == symbol && o.Ltp > 0)
            .OrderByDescending(o => o.CreatedOn)
            .Select(o => (decimal?)o.Ltp)
            .FirstOrDefaultAsync();
        var exitPrice = PaperFillPrice(decisionPrice, latestLtp, signal.OptionLtp > 0 ? signal.OptionLtp : signal.EntryPrice);

        // Prefer matching by SignalId (precise), fall back to OptionSymbol (best-effort)
        var signalId = signal.Id > 0 ? signal.Id : (long?)null;
        var trade = signalId.HasValue
            ? await db.PaperTrades.FirstOrDefaultAsync(
                t => t.SignalId == signalId && t.Status == TradingConst.TradeStatus.Open)
            : null;
        trade ??= await db.PaperTrades.FirstOrDefaultAsync(
                t => t.OptionSymbol == symbol && t.Status == TradingConst.TradeStatus.Open);

        if (trade is null)
            return Unique("PAPER-CLOSE-NOTFOUND");

        var closeLots = lotsToClose ?? trade.Quantity;
        if (closeLots <= 0)
            return Unique("PAPER-CLOSE-NOTHING");

        closeLots = Math.Min(closeLots, trade.Quantity);

        if (closeLots < trade.Quantity)
        {
            trade.Quantity -= closeLots;
        }
        else
        {
            trade.ExitPrice = exitPrice;
            trade.ProfitLoss = trade.Side == TradingConst.SignalType.CALL
                ? (exitPrice - trade.EntryPrice) * trade.Quantity
                : (trade.EntryPrice - exitPrice) * trade.Quantity;
            trade.Status = reason == "SL_HIT" ? "SL_HIT"
                              : reason == "TARGET_HIT" ? "TARGET_HIT"
                              : "CLOSED";
            trade.CloseReason = reason;
            trade.ClosedOn = IstClock.Now;
        }

        await db.SaveChangesAsync();
        return FormatCloseFillId(trade.Id, closeLots, exitPrice);
    }

    // Paper mode has no real open orders on broker side — these are all no-ops.
    // Unique per call: Orders.BrokerOrderId has a unique index.
    public Task<string> PlaceStopLossOrderAsync(Signal signal, decimal triggerPrice, int lots, string orderReferenceId)
        => Task.FromResult(Unique($"PAPER-SL-{signal.Id}"));
    public Task<bool> CancelOrderAsync(string orderId) => Task.FromResult(true);
    public Task<bool> ModifyOrderAsync(string orderId, decimal price) => Task.FromResult(true);
    public Task<List<string>> GetOpenOrdersAsync() => Task.FromResult(new List<string>());

    // ── Paper fills ──────────────────────────────────────────────────────────────────────────
    // Paper orders fill instantly. Reporting them as COMPLETE here lets BrokerOrderSyncWorker run
    // the SAME activation / full-close / partial-close path it runs for live orders. Previously
    // this returned null, so a paper entry stayed ORDER_PLACED forever, every exit was refused as
    // "entry never filled", and a "not found on broker" alert fired every cycle.
    // FilledQty is in CONTRACTS (lots x lot size), exactly as Groww reports it.
    public async Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId)
    {
        if (string.IsNullOrEmpty(brokerOrderId) || !brokerOrderId.StartsWith("PAPER-", StringComparison.Ordinal))
            return null;

        if (brokerOrderId.StartsWith("PAPER-SL-", StringComparison.Ordinal))
            return new BrokerOrderStatus { Status = "OPEN", StatusMessage = "paper stop (not simulated)" };

        if (brokerOrderId.StartsWith("PAPER-CLOSE-", StringComparison.Ordinal))
            return new BrokerOrderStatus { Status = "REJECTED", StatusMessage = brokerOrderId };   // NOTFOUND / NOTHING

        await using var db = await _dbFactory.CreateDbContextAsync();

        if (TryParseCloseFillId(brokerOrderId, out var closeTradeId, out var lots, out var price))
        {
            var closed = await db.PaperTrades.AsNoTracking().FirstOrDefaultAsync(t => t.Id == closeTradeId);
            var lotSize = await LotSizeAsync(db, closed?.Symbol);
            return new BrokerOrderStatus { Status = "COMPLETE", FilledQty = lots * lotSize, AvgPrice = price };
        }

        if (long.TryParse(brokerOrderId["PAPER-".Length..], out var tradeId))
        {
            var trade = await db.PaperTrades.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tradeId);
            if (trade is null)
                return new BrokerOrderStatus { Status = "REJECTED", StatusMessage = "paper trade not found" };

            var lotSize = await LotSizeAsync(db, trade.Symbol);
            var entryLots = trade.Quantity > 0 ? trade.Quantity : 1;
            return new BrokerOrderStatus { Status = "COMPLETE", FilledQty = entryLots * lotSize, AvgPrice = trade.EntryPrice };
        }

        return null;
    }

    private static async Task<int> LotSizeAsync(TradingDbContext db, string? symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return 1;
        var lot = await db.TradingInstruments.AsNoTracking()
            .Where(i => i.Symbol == symbol).Select(i => i.LotSize).FirstOrDefaultAsync();
        return lot > 0 ? lot : 1;
    }

    /// <summary>Paper fill price: decision price, else latest chain LTP, else entry.</summary>
    internal static decimal PaperFillPrice(decimal? decisionPrice, decimal? latestChainLtp, decimal entryPrice)
        => decisionPrice is > 0 ? decisionPrice.Value
         : latestChainLtp is > 0 ? latestChainLtp.Value
         : entryPrice;

    private static string Unique(string prefix)
    {
        var id = $"{prefix}-{Guid.NewGuid():N}";
        return id.Length > 50 ? id[..50] : id;
    }

    // "PAPER-X-{tradeId}-{lots}-{price}-{rand}" — carries the fill so status lookups need no extra
    // table, and is unique per close (Orders.BrokerOrderId is unique, max 50 chars).
    internal static string FormatCloseFillId(long tradeId, int lots, decimal price)
    {
        var id = $"PAPER-X-{tradeId}-{lots}-{price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}";
        return id.Length > 50 ? id[..50] : id;
    }

    internal static bool TryParseCloseFillId(string id, out long tradeId, out int lots, out decimal price)
    {
        tradeId = 0; lots = 0; price = 0;
        if (!id.StartsWith("PAPER-X-", StringComparison.Ordinal)) return false;
        var parts = id["PAPER-X-".Length..].Split('-');
        return parts.Length >= 3
            && long.TryParse(parts[0], out tradeId)
            && int.TryParse(parts[1], out lots)
            && decimal.TryParse(parts[2], System.Globalization.NumberStyles.Number,
                                System.Globalization.CultureInfo.InvariantCulture, out price);
    }
}
