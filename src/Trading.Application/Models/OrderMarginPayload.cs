using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class OrderMarginPayload
{
    [JsonPropertyName("required_margin")]
    public decimal? RequiredMargin { get; set; }

    [JsonPropertyName("span_margin")]
    public decimal? SpanMargin { get; set; }

    [JsonPropertyName("exposure_margin")]
    public decimal? ExposureMargin { get; set; }
}
