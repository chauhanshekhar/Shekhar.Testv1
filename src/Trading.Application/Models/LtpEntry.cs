using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class LtpEntry
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("ltp")]
    public decimal? Ltp { get; set; }
}
