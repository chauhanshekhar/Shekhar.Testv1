namespace Trading.Application.Models;

public class BrokerOrderStatus
{
    public string Status        { get; set; } = "";   // PENDING / OPEN / COMPLETE / REJECTED / CANCELLED
    public string StatusMessage { get; set; } = "";   // rejection reason etc.
    public int    FilledQty     { get; set; }
    public decimal AvgPrice     { get; set; }
}
