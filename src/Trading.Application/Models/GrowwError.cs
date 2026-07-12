using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class GrowwError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
