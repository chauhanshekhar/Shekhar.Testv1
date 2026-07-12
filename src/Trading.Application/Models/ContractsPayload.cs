using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class ContractsPayload
{
    [JsonPropertyName("contracts")]
    public List<string> Contracts { get; set; } = [];
}
