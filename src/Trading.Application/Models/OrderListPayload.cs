using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OrderListPayload
{
    [JsonPropertyName("orders")]
    public List<OrderDetail> Orders { get; set; } = [];
}
