using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class ModifyOcoRequest
{
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("net_position_quantity")]
    public int NetPositionQuantity { get; set; }

    [JsonPropertyName("target")]
    public OcoLegDetail Target { get; set; } = new();

    [JsonPropertyName("stop_loss")]
    public OcoLegDetail StopLoss { get; set; } = new();

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "DAY";
}
