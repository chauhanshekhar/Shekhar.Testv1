namespace Trading.Domain.Entities;

public class Signal
{
    public long Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string SignalType { get; set; } = string.Empty;

    public decimal EntryPrice { get; set; }

    public decimal StopLoss { get; set; }

    public decimal Target1 { get; set; }

    public decimal Target2 { get; set; }

    public decimal Confidence { get; set; }

    public DateTime CreatedOn { get; set; }

    // Option details
    public string OptionType { get; set; } = "";   // CE or PE
    public decimal StrikePrice { get; set; }
    public string OptionSymbol { get; set; } = "";   // e.g. NIFTY26JUN1923550CE (from Groww chain)
    public decimal OptionLtp { get; set; }
    public decimal OptionSL { get; set; }
    public decimal OptionT1 { get; set; }
    public decimal OptionT2 { get; set; }

    // Order execution
    public string Exchange { get; set; } = "NSE";     // NSE or BSE (SENSEX = BSE)
    public int Quantity { get; set; } = 1;          // lot size

    // Strategy
    public string StrategyType { get; set; } = "SINGLE"; // SINGLE / BULL_SPREAD / BEAR_SPREAD

    // Second leg (spreads: short OTM leg)
    public string Leg2OptionType { get; set; } = "";
    public decimal Leg2StrikePrice { get; set; }
    public string Leg2Symbol { get; set; } = "";
    public decimal Leg2Ltp { get; set; }
    public string Leg2OrderId { get; set; } = "";

    // Order tracking
    public string BrokerOrderId { get; set; } = "";   // Groww order ID for leg 1 (set after PlaceOrderAsync)
    public string OrderReferenceId { get; set; } = "";
    public string BrokerOrderStatus { get; set; } = "";
    public int OrderSyncAttempts { get; set; }
    public DateTime? LastOrderSyncTime { get; set; }

    // Partial exit tracking
    public int RemainingQty { get; set; }   // lots still open (initialized = Quantity)
    public decimal PartialPnL { get; set; }   // accumulated P&L from T1 partial exit

    // Order lifecycle status
    public string Status { get; set; } = SignalStatus.Pending;

    // Excursion tracking — updated each ExitWorker cycle while position is open
    public decimal MfePrice { get; set; }   // highest option LTP seen (max favourable excursion)
    public decimal MaePrice { get; set; }   // lowest option LTP seen (max adverse excursion)

    // Exit metadata
    public DateTime? ExitTime { get; set; }
    public string? ExitReason { get; set; }

    // Market context at entry — copied to TradeJournal on exit
    public decimal RsiAtEntry        { get; set; }
    public decimal PcrAtEntry        { get; set; }
    public decimal DeltaAtEntry      { get; set; }
    public decimal ChangeInOiAtEntry { get; set; }
    public decimal AtrPctAtEntry     { get; set; }
    public decimal AtrRawAtEntry     { get; set; } // Index ATR (points), frozen at entry — used by ExitWorker's ATR-based trailing SL
    public bool IndexT1Hit { get; set; } = false;

    public decimal ProtectedSL { get; set; } = 0;

    // Strategy/market context at entry — for StrategyPerformance bucketing (copied to TradeJournal on exit).
    public string SelectedStrategy { get; set; } = "";
    public string MarketState { get; set; } = "";
    public bool IsExpiryDay { get; set; }
}