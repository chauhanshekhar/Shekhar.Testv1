using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// Single shared JSON options + deserializer for every Groww API response. Groww sometimes
/// sends null (or a numeric value as a string) for fields the app previously modeled as
/// non-nullable decimal/int/long, which crashed deserialization — DTOs are now nullable and
/// this helper both tolerates string-encoded numbers and wraps any remaining JsonException
/// with the failing path for diagnosis.
/// </summary>
public static class GrowwJsonHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Groww JSON deserialize failed Path={ex.Path}",
                ex);
        }
    }
}
