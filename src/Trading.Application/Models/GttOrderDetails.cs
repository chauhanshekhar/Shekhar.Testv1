using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class GttOrderDetails
{
    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "LIMIT";

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = "";
}
