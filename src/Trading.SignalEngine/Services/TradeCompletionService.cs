using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;

namespace Trading.SignalEngine.Services;

   public class TradeCompletionService
{
    private readonly IRepository<TradeJournal> _journalRepo;
    private readonly IRepository<Order> _orderRepo;
    private readonly IRepository<Position> _positionRepo;
    private readonly IRepository<SignalOutcome> _outcomeRepo;
    private readonly IRepository<OptionChainData> _optionRepo;
    private readonly IInstrumentCache _instruments;
    private readonly IUnitOfWork _uow;


    public TradeCompletionService(
        IRepository<TradeJournal> journalRepo,
        IRepository<Order> orderRepo,
        IRepository<Position> positionRepo,
        IRepository<SignalOutcome> outcomeRepo,
        IRepository<OptionChainData> optionRepo,
        IInstrumentCache instruments,
        IUnitOfWork uow)
    {
        _journalRepo = journalRepo;
        _orderRepo = orderRepo;
        _positionRepo = positionRepo;
        _outcomeRepo = outcomeRepo;
        _optionRepo = optionRepo;
        _instruments = instruments;
        _uow = uow;
    }


    public async Task CompleteExitAsync(
        Signal signal,
        Order exitOrder,
        string exitReason)
    {

        var entryOrder =
            (await _orderRepo.FindAsync(
            x => x.SignalId == signal.Id &&
               x.Side == "BUY"))
            .OrderBy(x => x.OrderedAt)
            .FirstOrDefault();


        var lotSize =
            _instruments.GetLotSize(signal.Symbol);


        var qty =
            signal.RemainingQty > 0
            ? signal.RemainingQty
            : signal.Quantity;


        var entryPrice =
            signal.OptionLtp > 0
            ? signal.OptionLtp
            : signal.EntryPrice;


        var exitPrice =
            exitOrder.AvgPrice;


        var pnl =
            (exitPrice - entryPrice)
            * qty
            * lotSize;


        await _journalRepo.AddAsync(
        new TradeJournal
        {
            Symbol = signal.Symbol,

            TradeType =
            signal.SignalType,

            EntryPrice =
            entryPrice,

            ExitPrice =
            exitPrice,

            Quantity =
            qty,

            ProfitLoss =
            pnl,

            TradeDate =
            IstClock.Now,

            SignalId =
            signal.Id,

            EntryOrderId =
            entryOrder?.Id,

            ExitOrderId =
            exitOrder.Id,

            IsPartial =
            false,

            StrategyType =
            signal.StrategyType,

            ExitReason =
            exitReason,

            SignalConfidence =
            signal.Confidence,

            RsiAtEntry =
            signal.RsiAtEntry,

            PcrAtEntry =
            signal.PcrAtEntry,

            DeltaAtEntry =
            signal.DeltaAtEntry,

            ChangeInOiAtEntry =
            signal.ChangeInOiAtEntry
        });



        await _outcomeRepo.AddAsync(
        new SignalOutcome
        {
            SignalId =
            signal.Id,

            Symbol =
            signal.Symbol,

            StrategyType =
            signal.StrategyType,

            Result =
            pnl > 0
            ? "WIN"
            : pnl < 0
              ? "LOSS"
              : "BREAKEVEN",

            EntryPrice =
            entryPrice,

            ExitPrice =
            exitPrice,

            PnL =
            pnl,

            ExitReason =
            exitReason,

            CreatedOn =
            IstClock.Now
        });



        var positions =
        await _positionRepo.FindAsync(
        p =>
        p.SignalId == signal.Id
        &&
        p.Status == "OPEN"
        &&
        p.PositionSource == "STRATEGY");


        foreach (var p in positions)
        {
            p.Status = "CLOSED";
            p.ClosedAt = IstClock.Now;
            p.UpdatedOn = IstClock.Now;
        }


        signal.Status =
        SignalStatus.Closed;

        signal.RemainingQty = 0;

        signal.ExitTime =
        IstClock.Now;


        await _uow.SaveChangesAsync();
    }
    public decimal ComputeLeg2Pnl(
    Signal signal,
    int lots,
    int lotSize,
    Dictionary<string, List<OptionChainData>> chain)
    {
        if (signal.StrategyType == TradingConst.StrategyType.Single)
            return 0;

        if (!chain.TryGetValue(signal.Symbol, out var options))
            return 0;


        var leg2 = options.FirstOrDefault(x =>
            x.OptionType == signal.Leg2OptionType &&
            x.StrikePrice == signal.Leg2StrikePrice);


        if (leg2 == null)
            return 0;


        return
        (signal.Leg2Ltp - leg2.Ltp)
        * lots
        * lotSize;
    }
    public async Task CompletePartialExitAsync(
    Signal signal,
    Order partialExitOrder)
    {
        // prevent duplicate broker sync execution
        var existing =
            await _journalRepo.FirstOrDefaultAsync(
                j => j.SignalId == signal.Id
                  && j.ExitOrderId == partialExitOrder.Id
                  && j.IsPartial);

        if (existing != null)
            return;


        var entryOrder =
            (await _orderRepo.FindAsync(
                x => x.SignalId == signal.Id
                  && x.Side == "BUY"))
            .OrderBy(x => x.OrderedAt)
            .FirstOrDefault();


        var closeLots =
            partialExitOrder.FilledQty > 0
            ? partialExitOrder.FilledQty
            : partialExitOrder.Quantity;


        var lotSize =
            _instruments.GetLotSize(signal.Symbol);


        var entryPrice =
            signal.OptionLtp > 0
            ? signal.OptionLtp
            : signal.EntryPrice;


        var exitPrice =
            partialExitOrder.AvgPrice;


        var partialPnl =
            (exitPrice - entryPrice)
            *
            closeLots
            *
            lotSize;


        // add previous pnl
        signal.PartialPnL += partialPnl;


        // reduce remaining lots AFTER broker confirmation
        var currentQty =
            signal.RemainingQty > 0
            ? signal.RemainingQty
            : signal.Quantity;


        signal.RemainingQty =
            Math.Max(
                currentQty - closeLots,
                0);


        // move SL to breakeven after T1 booking
        signal.OptionSL =
            signal.OptionLtp;


        // trade still running
        signal.Status =
            SignalStatus.Active;


        await _journalRepo.AddAsync(
            new TradeJournal
            {
                Symbol =
                    signal.Symbol,

                TradeType =
                    signal.SignalType,

                EntryPrice =
                    entryPrice,

                ExitPrice =
                    exitPrice,

                Quantity =
                    closeLots,

                ProfitLoss =
                    partialPnl,

                TradeDate =
                    IstClock.Now,

                Remarks =
                    $"PARTIAL_{signal.Id}",

                SignalId =
                    signal.Id,

                EntryOrderId =
                    entryOrder?.Id,

                ExitOrderId =
                    partialExitOrder.Id,

                IsPartial =
                    true,

                StrategyType =
                    signal.StrategyType,

                ExitReason =
                    TradingConst.ExitReason.OptionT1Hit,

                SignalConfidence =
                    signal.Confidence,

                RsiAtEntry =
                    signal.RsiAtEntry,

                PcrAtEntry =
                    signal.PcrAtEntry,

                DeltaAtEntry =
                    signal.DeltaAtEntry,

                ChangeInOiAtEntry =
                    signal.ChangeInOiAtEntry
            });


        await _uow.SaveChangesAsync();
    }
}

