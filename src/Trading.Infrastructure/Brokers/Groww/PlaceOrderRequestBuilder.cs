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

    // ── Convenience: wire everything from a Signal for an ENTRY order ─────────

    public PlaceOrderRequestBuilder FromSignalEntry(Signal signal, int contractQty, string? orderReferenceId = null)
    {
        _tradingSymbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol)
                           ? signal.OptionSymbol
                           : signal.Symbol;
        _quantity = contractQty;
        _price = signal.OptionLtp > 0 ? signal.OptionLtp : signal.EntryPrice;
        _exchange        = signal.Exchange == "BSE" ? GrowwExchange.Bse : GrowwExchange.Nse;
        _transactionType = GrowwTransactionType.Buy;   // always BUY the option (CALL or PUT)
        _orderType       = GrowwOrderType.Limit;
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
        Exchange = _exchange,
        Segment = _segment,
        Product = _product,
        OrderType = _orderType,
        TransactionType = _transactionType,
        Validity = _validity,
        OrderReferenceId = _orderReferenceId
    };
}
