using System.Text.Json.Serialization;

namespace Trading.API.Controllers;

public class OrderPostbackDto
{
    [JsonPropertyName("orderid")] public string OrderId { get; set; } = "";
    [JsonPropertyName("tradingsymbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("exchange")] public string Exchange { get; set; } = "";
    [JsonPropertyName("transactiontype")] public string Side { get; set; } = "";
    [JsonPropertyName("ordertype")] public string OrderType { get; set; } = "";
    [JsonPropertyName("producttype")] public string ProductType { get; set; } = "";
    [JsonPropertyName("orderstatus")] public string Status { get; set; } = "";
    [JsonPropertyName("text")] public string StatusMessage { get; set; } = "";
    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("triggerprice")] public decimal TriggerPrice { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; }
    [JsonPropertyName("fillshares")] public int FilledQty { get; set; }
    [JsonPropertyName("averageprice")] public decimal AvgPrice { get; set; }
    [JsonPropertyName("ordertime")] public string OrderTime { get; set; } = "";
    [JsonPropertyName("updatetime")] public string UpdateTime { get; set; } = "";
}
