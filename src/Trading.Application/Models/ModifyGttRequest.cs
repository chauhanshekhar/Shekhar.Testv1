using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class ModifyGttRequest
{
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("trigger_price")]
    public string TriggerPrice { get; set; } = "";

    [JsonPropertyName("trigger_direction")]
    public string TriggerDirection { get; set; } = "UP";

    [JsonPropertyName("order")]
    public GttOrderDetails Order { get; set; } = new();

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "DAY";
}
