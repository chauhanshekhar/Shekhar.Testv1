using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class SmartOrderDetail
{
    [JsonPropertyName("smart_order_id")]
    public string SmartOrderId { get; set; } = "";

    [JsonPropertyName("smart_order_type")]
    public string SmartOrderType { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "";

    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "";

    [JsonPropertyName("product_type")]
    public string ProductType { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("trigger_price")]
    public string? TriggerPrice { get; set; }

    [JsonPropertyName("trigger_direction")]
    public string? TriggerDirection { get; set; }

    [JsonPropertyName("order")]
    public GttOrderDetails? Order { get; set; }

    [JsonPropertyName("target")]
    public OcoLegDetail? Target { get; set; }

    [JsonPropertyName("stop_loss")]
    public OcoLegDetail? StopLoss { get; set; }

    [JsonPropertyName("transaction_type")]
    public string? TransactionType { get; set; }

    [JsonPropertyName("net_position_quantity")]
    public int? NetPositionQuantity { get; set; }

    [JsonPropertyName("is_cancellation_allowed")]
    public bool IsCancellationAllowed { get; set; }

    [JsonPropertyName("is_modification_allowed")]
    public bool IsModificationAllowed { get; set; }

    [JsonPropertyName("expire_at")]
    public string? ExpireAt { get; set; }

    [JsonPropertyName("triggered_at")]
    public string? TriggeredAt { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}
