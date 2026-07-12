using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class ModifyOrderRequest
{
    [JsonPropertyName("groww_order_id")]
    public string GrowwOrderId { get; set; } = "";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "FNO";

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "LIMIT";

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("trigger_price")]
    public decimal? TriggerPrice { get; set; }
}
