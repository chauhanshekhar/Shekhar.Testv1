namespace Trading.Domain.Entities;

public class Candle
{
    public long Id { get; set; }

    public string Symbol { get; set; } = "";

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public long Volume { get; set; }

    public long OpenInterest { get; set; }

    public DateTime CandleTime { get; set; }
}