using Trading.Domain;
using Microsoft.EntityFrameworkCore;
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

    public PaperOrderStrategy(IDbContextFactory<TradingDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<string> PlaceOrderAsync(Signal signal)
    {
        var trade = new PaperTrade
        {
            SignalId         = signal.Id > 0 ? signal.Id : null,
            Symbol           = signal.Symbol,
            OptionSymbol     = signal.OptionSymbol,
            Side             = signal.SignalType,
            StrategyType     = signal.StrategyType,
            Quantity         = signal.Quantity > 0 ? signal.Quantity : 1,
            EntryPrice       = signal.OptionLtp > 0 ? signal.OptionLtp : signal.EntryPrice,
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

    public async Task<string> ClosePositionAsync(Signal signal, string reason, int? lotsToClose = null, string? orderReferenceId = null)
    {
        var exitPrice = signal.OptionLtp > 0 ? signal.OptionLtp : signal.EntryPrice;
        var symbol = !string.IsNullOrEmpty(signal.OptionSymbol)
                        ? signal.OptionSymbol
                        : signal.Symbol;

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Prefer matching by SignalId (precise), fall back to OptionSymbol (best-effort)
        var signalId = signal.Id > 0 ? signal.Id : (long?)null;
        var trade = signalId.HasValue
            ? await db.PaperTrades.FirstOrDefaultAsync(
                t => t.SignalId == signalId && t.Status == TradingConst.TradeStatus.Open)
            : null;
        trade ??= await db.PaperTrades.FirstOrDefaultAsync(
                t => t.OptionSymbol == symbol && t.Status == TradingConst.TradeStatus.Open);

        if (trade is null)
            return "PAPER-CLOSE-NOTFOUND";

        var closeLots = lotsToClose ?? trade.Quantity;
        if (closeLots <= 0)
            return "PAPER-CLOSE-NOTHING";

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
        return $"PAPER-CLOSE-{trade.Id}";
    }

    // Paper mode has no real open orders on broker side — these are all no-ops.
    public Task<string> PlaceStopLossOrderAsync(Signal signal, decimal triggerPrice, int lots, string orderReferenceId)
        => Task.FromResult($"PAPER-SL-{signal.Id}");
    public Task<bool> CancelOrderAsync(string orderId) => Task.FromResult(true);
    public Task<bool> ModifyOrderAsync(string orderId, decimal price) => Task.FromResult(true);
    public Task<List<string>> GetOpenOrdersAsync() => Task.FromResult(new List<string>());
    public Task<BrokerOrderStatus?> GetOrderStatusAsync(string brokerOrderId) => Task.FromResult<BrokerOrderStatus?>(null);
}
