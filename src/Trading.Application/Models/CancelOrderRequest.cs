using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class CancelOrderRequest
{
    [JsonPropertyName("groww_order_id")]
    public string GrowwOrderId { get; set; } = "";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "FNO";
}
