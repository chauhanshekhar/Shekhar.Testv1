namespace Trading.Domain.Entities;

public class Position
{
    public long Id { get; set; }

    public string Symbol { get; set; } = "";

    public int Quantity { get; set; }

    public decimal AvgPrice { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal PnL { get; set; }

    public DateTime UpdatedOn { get; set; }

    // Signal-linked lifecycle (PositionSource = STRATEGY). Null/BROKER_SYNC rows are the
    // plain broker-mirror upserted by BrokerPositionSyncWorker — the two populations never
    // overlap: BROKER_SYNC rows always have SignalId == null.
    public long? SignalId { get; set; }
    public long? EntryOrderId { get; set; }
    public string OptionSymbol { get; set; } = "";
    public string Status { get; set; } = "OPEN";
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string PositionSource { get; set; } = "BROKER_SYNC";
}
