using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OptionLegData
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("ltp")]
    public decimal? Ltp { get; set; }

    [JsonPropertyName("open_interest")]
    public long? OpenInterest { get; set; }

    [JsonPropertyName("volume")]
    public long? Volume { get; set; }

    [JsonPropertyName("greeks")]
    public OptionGreeks? Greeks { get; set; }
}
