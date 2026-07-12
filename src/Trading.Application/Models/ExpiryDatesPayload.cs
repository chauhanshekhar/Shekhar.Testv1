using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class ExpiryDatesPayload
{
    [JsonPropertyName("expiries")]
    public List<string> ExpiryDates { get; set; } = [];
}
