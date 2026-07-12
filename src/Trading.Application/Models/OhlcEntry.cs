using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OhlcEntry
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("ltp")]
    public decimal? Ltp { get; set; }

    [JsonPropertyName("open")]
    public decimal? Open { get; set; }

    [JsonPropertyName("high")]
    public decimal? High { get; set; }

    [JsonPropertyName("low")]
    public decimal? Low { get; set; }

    [JsonPropertyName("close")]
    public decimal? Close { get; set; }

    [JsonPropertyName("volume")]
    public long? Volume { get; set; }
}
