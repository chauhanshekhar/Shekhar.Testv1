using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class PlaceOrderResponse
{
    [JsonPropertyName("groww_order_id")]
    public string GrowwOrderId { get; set; } = "";

    [JsonPropertyName("order_status")]
    public string OrderStatus { get; set; } = "";

    [JsonPropertyName("order_reference_id")]
    public string? OrderReferenceId { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }
}
