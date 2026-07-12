using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class CreateGttRequest
{
    [JsonPropertyName("reference_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceId { get; set; }

    [JsonPropertyName("smart_order_type")]
    public string SmartOrderType { get; set; } = "GTT";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "FNO";

    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("trigger_price")]
    public string TriggerPrice { get; set; } = "";

    [JsonPropertyName("trigger_direction")]
    public string TriggerDirection { get; set; } = "UP";

    [JsonPropertyName("order")]
    public GttOrderDetails Order { get; set; } = new();

    [JsonPropertyName("product_type")]
    public string ProductType { get; set; } = "CNC";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "NSE";

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "DAY";
}
