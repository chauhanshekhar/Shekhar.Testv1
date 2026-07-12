using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class UserDetailPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = "";

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "";

    [JsonPropertyName("exchanges")]
    public List<string> Exchanges { get; set; } = [];

    [JsonPropertyName("segments")]
    public List<string> Segments { get; set; } = [];

    [JsonPropertyName("products")]
    public List<string> Products { get; set; } = [];
}
