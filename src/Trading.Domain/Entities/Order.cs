namespace Trading.Domain.Entities;

public class Order
{
    public long Id { get; set; }
    public long? SignalId { get; set; }
    public string OrderReferenceId { get; set; } = "";
    public string Broker { get; set; } = "GROWW";
    public string BrokerOrderId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Exchange { get; set; } = "";
    public string Side { get; set; } = "";   // BUY / SELL
    public string OrderType { get; set; } = "";   // MARKET / LIMIT / SL / SL-M
    public string ProductType { get; set; } = "";   // CNC / MIS / NRML
    public string Status { get; set; } = "";   // see Trading.Domain.OrderStatus for our lifecycle states; also stores Groww-reported states (NEW/ACKED/TRIGGER_PENDING/APPROVED/EXECUTED/COMPLETED/REJECTED/CANCELLED)
    public string StatusMessage { get; set; } = "";   // rejection reason etc.
    public decimal Price { get; set; }
    public decimal TriggerPrice { get; set; }
    public int Quantity { get; set; }
    public int FilledQty { get; set; }
    public decimal AvgPrice { get; set; }
    public DateTime OrderedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string RawPostback { get; set; } = "";   // full JSON from Groww for audit
}
