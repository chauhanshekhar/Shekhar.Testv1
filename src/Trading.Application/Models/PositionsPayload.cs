using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class PositionsPayload
{
    [JsonPropertyName("positions")]
    public List<PositionEntry> Positions { get; set; } = [];
}
