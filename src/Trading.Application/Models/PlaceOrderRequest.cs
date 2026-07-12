using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class PlaceOrderRequest
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    // Only sent for SL / SL_M order types — omitted (default 0) for LIMIT and MARKET.
    [JsonPropertyName("trigger_price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public decimal TriggerPrice { get; set; }

    [JsonPropertyName("disclosed_quantity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DisclosedQuantity { get; set; }

    [JsonPropertyName("validity")]
    public string Validity { get; set; } = "DAY";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "NSE";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "FNO";

    [JsonPropertyName("product")]
    public string Product { get; set; } = "MIS";

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "LIMIT";

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = "";

    [JsonPropertyName("order_reference_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrderReferenceId { get; set; }
}
