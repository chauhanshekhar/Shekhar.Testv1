using System.Text.Json.Serialization;

namespace Trading.Application.Models;

// Represents one strike in the Groww option chain response.
// The strike price comes from the dictionary key, not from this object.
public class OptionChainEntry
{
    [JsonPropertyName("CE")]
    public OptionLegData? CE { get; set; }

    [JsonPropertyName("PE")]
    public OptionLegData? PE { get; set; }
}
