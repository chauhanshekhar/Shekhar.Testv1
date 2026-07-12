using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class HoldingsPayload
{
    [JsonPropertyName("holdings")]
    public List<HoldingEntry> Holdings { get; set; } = [];
}
