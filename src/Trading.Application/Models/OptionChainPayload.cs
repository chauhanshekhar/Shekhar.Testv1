using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OptionChainPayload
{
    [JsonPropertyName("underlying_ltp")]
    public decimal? UnderlyingLtp { get; set; }

    // Key = strike price as string (e.g. "23400"), Value = CE/PE legs for that strike.
    [JsonPropertyName("strikes")]
    public Dictionary<string, OptionChainEntry> Strikes { get; set; } = [];
}
