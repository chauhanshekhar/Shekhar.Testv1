using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class SmartOrderListPayload
{
    [JsonPropertyName("orders")]
    public List<SmartOrderDetail> Orders { get; set; } = [];
}
