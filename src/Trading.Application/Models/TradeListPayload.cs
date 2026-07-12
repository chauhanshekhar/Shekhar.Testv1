using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class TradeListPayload
{
    [JsonPropertyName("trades")]
    public List<TradeDetail> Trades { get; set; } = [];
}
