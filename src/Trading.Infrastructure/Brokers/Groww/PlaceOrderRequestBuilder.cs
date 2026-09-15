using Trading.Application.Models;
using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// Builder for PlaceOrderRequest — eliminates duplicate object construction
/// between entry orders and exit/close orders.
/// </summary>
public class PlaceOrderRequestBuilder
{
    private string _tradingSymbol = "";
    private int _quantity = 1;
    private decimal _price = 0;
    private decimal _triggerPrice = 0;
    private string _exchange = GrowwExchange.Nse;
    private string _segment = GrowwSegment.Fno;
    private string _product = GrowwProduct.Mis;
    private string _orderType = GrowwOrderType.Limit;
    private string _transactionType = GrowwTransactionType.Buy;
    private string _validity = GrowwValidity.Day;
    private string _orderReferenceId = Guid.NewGuid().ToString("N")[..20];

    // ── Fluent setters ────────────────────────────────────────────────────────

    public PlaceOrderRequestBuilder ForSymbol(string symbol)
    { _tradingSymbol = symbol; return this; }

    public PlaceOrderRequestBuilder WithQuantity(int qty)
    { _quantity = qty; return this; }

    public PlaceOrderRequestBuilder AtPrice(decimal price)
    { _price = price; return this; }

    public PlaceOrderRequestBuilder OnExchange(string exchange)
    { _exchange = exchange; return this; }

    public PlaceOrderRequestBuilder AsBuy()
    { _transactionType = GrowwTransactionType.Buy; return this; }

    public PlaceOrderRequestBuilder AsSell()
    { _transactionType = GrowwTransactionType.Sell; return this; }

    public PlaceOrderRequestBuilder AsMarket()
    { _orderType = GrowwOrderType.Market; _price = 0; return this; }

    public PlaceOrderRequestBuilder AsLimit()
    { _orderType = GrowwOrderType.Limit; return this; }

    public PlaceOrderRequestBuilder WithTriggerPrice(decimal triggerPrice)
    { _triggerPrice = triggerPrice; return this; }

    // ── Convenience: wire everything from a Signal for an ENTRY order ─────────

    /// <param name="useMarketOrder">
    /// MARKET fills immediately; LIMIT rests at the premium quoted when the signal was built and
    /// simply never fills if the option ticks away from it — which on a slow placement round-trip
    /// means a missed entry rather than a bad one. Same shape as FromSignalExit, which has always
    /// used MARKET with price 0.
    ///
    /// Slippage is bounded elsewhere: SpreadGate rejects the signal before it reaches here if the
    /// chosen leg's bid/ask is too wide, and BrokerOrderSyncWorker.ActivateSignalAsync re-anchors
    /// OptionSL/OptionT1/OptionT2 to the ACTUAL fill price, preserving the intended risk/reward
    /// ratio — so a fill away from the quote moves the stop with it instead of silently tightening
    /// the real risk.
    /// </param>
    public PlaceOrderRequestBuilder FromSignalEntry(
        Signal signal, int contractQty, string? orderReferenceId = null, bool useMarketOrder = false)
    {
        _tradingSymbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol)
                           ? signal.OptionSymbol
                           : signal.Symbol;
        _quantity = contractQty;
        _price = useMarketOrder
            ? 0                                    // MARKET carries no price, as FromSignalExit does
            : signal.OptionLtp > 0 ? signal.OptionLtp : signal.EntryPrice;
        _exchange        = signal.Exchange == "BSE" ? GrowwExchange.Bse : GrowwExchange.Nse;
        _transactionType = GrowwTransactionType.Buy;   // always BUY the option (CALL or PUT)
        _orderType       = useMarketOrder ? GrowwOrderType.Market : GrowwOrderType.Limit;
        if (!string.IsNullOrEmpty(orderReferenceId)) _orderReferenceId = orderReferenceId;
        return this;
    }

    // ── Convenience: wire everything from a Signal for a CLOSE/EXIT order ─────

    public PlaceOrderRequestBuilder FromSignalExit(Signal signal, int contractQty, string? orderReferenceId = null)
    {
        _tradingSymbol   = !string.IsNullOrWhiteSpace(signal.OptionSymbol)
                           ? signal.OptionSymbol
                           : signal.Symbol;
        _quantity        = contractQty;
        _price           = 0;
        _exchange        = signal.Exchange == "BSE" ? GrowwExchange.Bse : GrowwExchange.Nse;
        _transactionType = GrowwTransactionType.Sell;  // always SELL to close the bought option
        _orderType       = GrowwOrderType.Market;
        if (!string.IsNullOrEmpty(orderReferenceId)) _orderReferenceId = orderReferenceId;
        return this;
    }

    // ── Convenience: wire everything from a Signal for a broker-managed SL_M stop loss ────

    public PlaceOrderRequestBuilder FromSignalStopLoss(Signal signal, int contractQty, decimal triggerPrice, string? orderReferenceId = null)
    {
        _tradingSymbol   = !string.IsNullOrWhiteSpace(signal.OptionSymbol)
                           ? signal.OptionSymbol
                           : signal.Symbol;
        _quantity        = contractQty;
        _price           = 0;
        _triggerPrice    = triggerPrice;
        _exchange        = signal.Exchange == "BSE" ? GrowwExchange.Bse : GrowwExchange.Nse;
        _transactionType = GrowwTransactionType.Sell;  // SELL to protect the bought option
        _orderType       = GrowwOrderType.SlM;
        if (!string.IsNullOrEmpty(orderReferenceId)) _orderReferenceId = orderReferenceId;
        return this;
    }

    // Spread short leg: SELL on entry
    public PlaceOrderRequestBuilder FromSpreadShortEntry(Signal signal, int contractQty, string? orderReferenceId = null)
    {
        _tradingSymbol = signal.Leg2Symbol;
        _quantity = contractQty;
        _price = signal.Leg2Ltp;
        _exchange = signal.Exchange == "BSE" ? GrowwExchange.Bse : GrowwExchange.Nse;
        _transactionType = GrowwTransactionType.Sell;
        _orderType = GrowwOrderType.Limit;
        if (!string.IsNullOrEmpty(orderReferenceId)) _orderReferenceId = orderReferenceId;
        return this;
    }

    // Spread short leg: BUY back on exit
    public PlaceOrderRequestBuilder FromSpreadShortExit(Signal signal, int contractQty, string? orderReferenceId = null)
    {
        _tradingSymbol = signal.Leg2Symbol;
        _quantity = contractQty;
        _price = 0;
        _exchange = signal.Exchange == "BSE" ? GrowwExchange.Bse : GrowwExchange.Nse;
        _transactionType = GrowwTransactionType.Buy;
        _orderType = GrowwOrderType.Market;
        if (!string.IsNullOrEmpty(orderReferenceId)) _orderReferenceId = orderReferenceId;
        return this;
    }

    public PlaceOrderRequest Build() => new()
    {
        TradingSymbol = _tradingSymbol,
        Quantity = _quantity,
        Price = _price,
        TriggerPrice = _triggerPrice,
        Exchange = _exchange,
        Segment = _segment,
        Product = _product,
        OrderType = _orderType,
        TransactionType = _transactionType,
        Validity = _validity,
        OrderReferenceId = _orderReferenceId
    };
}
