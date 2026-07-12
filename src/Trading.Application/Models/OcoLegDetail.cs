using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OcoLegDetail
{
    [JsonPropertyName("trigger_price")]
    public decimal? TriggerPrice { get; set; }

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "LIMIT";

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }
}
