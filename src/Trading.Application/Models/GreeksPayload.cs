using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class GreeksPayload
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("delta")]
    public decimal? Delta { get; set; }

    [JsonPropertyName("gamma")]
    public decimal? Gamma { get; set; }

    [JsonPropertyName("theta")]
    public decimal? Theta { get; set; }

    [JsonPropertyName("vega")]
    public decimal? Vega { get; set; }

    [JsonPropertyName("rho")]
    public decimal? Rho { get; set; }

    [JsonPropertyName("iv")]
    public decimal? Iv { get; set; }
}
