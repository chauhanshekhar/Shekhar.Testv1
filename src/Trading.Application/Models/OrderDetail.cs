using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OrderDetail
{
    [JsonPropertyName("groww_order_id")]
    public string GrowwOrderId { get; set; } = "";

    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "";

    [JsonPropertyName("product")]
    public string Product { get; set; } = "";

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "";

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = "";

    [JsonPropertyName("validity")]
    public string Validity { get; set; } = "";

    [JsonPropertyName("order_status")]
    public string? OrderStatus { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("disclosed_quantity")]
    public int? DisclosedQuantity { get; set; }

    [JsonPropertyName("filled_quantity")]
    public int? FilledQuantity { get; set; }

    [JsonPropertyName("remaining_quantity")]
    public int? RemainingQuantity { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("trigger_price")]
    public decimal? TriggerPrice { get; set; }

    [JsonPropertyName("average_fill_price")]
    public decimal? AverageFillPrice { get; set; }

    [JsonPropertyName("deliverable_quantity")]
    public int? DeliverableQuantity { get; set; }

    [JsonPropertyName("order_reference_id")]
    public string? OrderReferenceId { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("amo_status")]
    public string? AmoStatus { get; set; }

    [JsonPropertyName("trade_date")]
    public string? TradeDate { get; set; }

    [JsonPropertyName("exchange_time")]
    public string? ExchangeTime { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}
