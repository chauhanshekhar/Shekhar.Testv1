using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.SignalEngine.Services;

// Manages paper trade lifecycle: open from signal, close with P&L, stats.
// All state is persisted to the DB via IRepository<PaperTrade>.
public class PaperTradingService
{
    private readonly IRepository<PaperTrade> _repo;
    private readonly IUnitOfWork _uow;

    public PaperTradingService(IRepository<PaperTrade> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<PaperTrade> OpenAsync(
        Signal signal,
        int quantity = 0)
    {
        var trade = new PaperTrade
        {
            SignalId = signal.Id > 0 ? signal.Id : null,
            Symbol = signal.Symbol,
            OptionSymbol = signal.OptionSymbol,
            Side = signal.SignalType,
            StrategyType = signal.StrategyType,
            Leg2OptionSymbol = signal.Leg2Symbol ?? "",
            Leg2EntryPrice = signal.Leg2Ltp,

            Quantity = quantity > 0
                ? quantity
                : signal.Quantity,

            EntryPrice = signal.OptionLtp > 0
                ? signal.OptionLtp
                : signal.EntryPrice,

            StopLoss = signal.OptionSL > 0
                ? signal.OptionSL
                : signal.StopLoss,

            Target1 = signal.OptionT1 > 0
                ? signal.OptionT1
                : signal.Target1,

            Target2 = signal.OptionT2 > 0
                ? signal.OptionT2
                : signal.Target2,

            Status = TradingConst.TradeStatus.Open,
            CreatedOn = IstClock.Now
        };

        await _repo.AddAsync(trade);
        await _uow.SaveChangesAsync();

        return trade;
    }

    public async Task<PaperTrade?> CloseAsync(
        long id,
        decimal exitPrice,
        string reason = "MANUAL",
        decimal leg2ExitPrice = 0m)
    {
        var trade = await _repo.FirstOrDefaultAsync(t => t.Id == id);

        if (trade == null || trade.Status != TradingConst.TradeStatus.Open)
            return null;

        trade.ExitPrice = exitPrice;
        trade.ProfitLoss = ComputePnL(trade, exitPrice, leg2ExitPrice);
        trade.Status = reason switch
        {
            "SL_HIT"     => TradingConst.TradeStatus.SlHit,
            "TARGET_HIT" => TradingConst.TradeStatus.TargetHit,
            _            => TradingConst.TradeStatus.Closed
        };
        trade.CloseReason = reason;
        trade.ClosedOn = IstClock.Now;

        await _repo.UpdateAsync(trade);
        await _uow.SaveChangesAsync();

        return trade;
    }

    private static decimal ComputePnL(PaperTrade trade, decimal leg1Exit, decimal leg2Exit)
    {
        var leg1Pnl = (leg1Exit - trade.EntryPrice) * trade.Quantity;

        // Only add leg2 P&L when both entry and exit prices are known
        if (trade.Leg2EntryPrice == 0 || leg2Exit == 0)
            return trade.StrategyType == TradingConst.StrategyType.Single &&
        trade.Side != TradingConst.SignalType.CALL
     ? (trade.EntryPrice - leg1Exit) * trade.Quantity
     : leg1Pnl;

        var leg2Pnl = (trade.Leg2EntryPrice - leg2Exit) * trade.Quantity;  // short leg (spread)

        return leg1Pnl + leg2Pnl;
    }

    // Close the first open paper trade for a given option symbol (used by auto-exit logic).
    public async Task<PaperTrade?> CloseBySymbolAsync(
        string optionSymbol, decimal exitPrice, string reason = "AUTO", decimal leg2ExitPrice = 0m)
    {
        var open = await _repo.FirstOrDefaultAsync(
            t => t.OptionSymbol == optionSymbol && t.Status == TradingConst.TradeStatus.Open);

        if (open is null) return null;
        return await CloseAsync(open.Id, exitPrice, reason, leg2ExitPrice);
    }

    public async Task<List<PaperTrade>> GetOpenTradesAsync()
        => await _repo.FindAsync(t => t.Status == TradingConst.TradeStatus.Open);

    public async Task<List<PaperTrade>> GetAllTradesAsync()
        => await _repo.GetAllAsync();

    // Build live position snapshots for all open trades.
    // ltpMap: optionSymbol → current market LTP (caller fetches this from OptionChainData / MarketTick).
    public async Task<List<PaperPositionSnapshot>> GetLiveSnapshotsAsync(
        IReadOnlyDictionary<string, decimal> ltpMap)
    {
        var open = await GetOpenTradesAsync();
        return open.Select(t =>
        {
            ltpMap.TryGetValue(t.OptionSymbol, out var ltp);
            ltpMap.TryGetValue(t.Leg2OptionSymbol, out var leg2Ltp);

            var unrealised = ltp > 0 ? ComputePnL(t, ltp, leg2Ltp) : 0m;

            // Cost basis = combined entry value for spreads
            var entryValue = t.StrategyType switch
            {
                TradingConst.StrategyType.BullSpread or TradingConst.StrategyType.BearSpread
                    => Math.Abs(t.EntryPrice - t.Leg2EntryPrice),
                _   => t.EntryPrice,
            };
            var cost = entryValue * t.Quantity;

            return new PaperPositionSnapshot
            {
                Id = t.Id,
                Symbol = t.Symbol,
                OptionSymbol = t.OptionSymbol,
                Side = t.Side,
                Quantity = t.Quantity,
                EntryPrice = t.EntryPrice,
                StopLoss = t.StopLoss,
                Target1 = t.Target1,
                Target2 = t.Target2,
                CurrentLtp = ltp,
                UnrealisedPnL = unrealised,
                PnLPercent = cost > 0 ? Math.Round(unrealised / cost * 100, 2) : 0,
                Status = t.Status,
                CreatedOn = t.CreatedOn,
            };
        }).ToList();
    }

    public PaperTradingStats GetStats(
        IEnumerable<PaperTrade> trades)
    {
        var closed =
            trades
            .Where(t => t.Status != TradingConst.TradeStatus.Open)
            .ToList();

        var open =
            trades
            .Where(t => t.Status == TradingConst.TradeStatus.Open)
            .ToList();

        var wins =
            closed.Count(t => t.ProfitLoss > 0);

        return new PaperTradingStats
        {
            TotalTrades = closed.Count,

            OpenTrades = open.Count,

            WinRate =
                closed.Count == 0
                    ? 0
                    : Math.Round(
                        (decimal)wins /
                        closed.Count * 100m,
                        2),

            TotalPnL =
                closed.Sum(t => t.ProfitLoss),

            BestTrade =
                closed.Count == 0
                    ? 0
                    : closed.Max(t => t.ProfitLoss),

            WorstTrade =
                closed.Count == 0
                    ? 0
                    : closed.Min(t => t.ProfitLoss),

            UnrealisedPnL = 0
        };
    }
}

public class PaperTradingStats
{
    public int TotalTrades { get; set; }
    public int OpenTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal BestTrade { get; set; }
    public decimal WorstTrade { get; set; }
    public decimal UnrealisedPnL { get; set; }
}
