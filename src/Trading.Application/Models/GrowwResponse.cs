using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class GrowwResponse<T>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("payload")]
    public T? Payload { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("error")]
    public GrowwError? Error { get; set; }

    public bool IsSuccess => Status?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true;
}
