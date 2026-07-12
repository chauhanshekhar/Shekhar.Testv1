using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class CreateOcoRequest
{
    [JsonPropertyName("reference_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReferenceId { get; set; }

    [JsonPropertyName("smart_order_type")]
    public string SmartOrderType { get; set; } = "OCO";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "FNO";

    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("net_position_quantity")]
    public int NetPositionQuantity { get; set; }

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = "";

    [JsonPropertyName("target")]
    public OcoLegDetail Target { get; set; } = new();

    [JsonPropertyName("stop_loss")]
    public OcoLegDetail StopLoss { get; set; } = new();

    [JsonPropertyName("product_type")]
    public string ProductType { get; set; } = "CNC";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "NSE";

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "DAY";
}
