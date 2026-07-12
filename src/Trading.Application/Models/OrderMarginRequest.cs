using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OrderMarginRequest
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "NSE";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "FNO";

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("product")]
    public string Product { get; set; } = "MIS";

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "LIMIT";
}
