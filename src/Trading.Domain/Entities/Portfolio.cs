namespace Trading.Domain.Entities;

public class Portfolio
{
    public long Id { get; set; }

    public decimal Capital { get; set; }

    public decimal UsedMargin { get; set; }

    public decimal AvailableMargin { get; set; }

    public decimal NetPnL { get; set; }

    public DateTime UpdatedOn { get; set; }
}