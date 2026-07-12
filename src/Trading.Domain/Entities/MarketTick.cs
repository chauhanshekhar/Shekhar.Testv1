namespace Trading.Domain.Entities;

public class MarketTick
{
    public long Id { get; set; }

    public string Symbol { get; set; } = "";

    public decimal LastPrice { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public long Volume { get; set; }

    public DateTime TickTime { get; set; }
}