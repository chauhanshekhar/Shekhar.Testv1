using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class MarginDetailPayload
{
    [JsonPropertyName("clear_cash")]
    public decimal? ClearCash { get; set; }

    [JsonPropertyName("collateral_available")]
    public decimal? CollateralAvailable { get; set; }

    [JsonPropertyName("collateral_used")]
    public decimal? CollateralUsed { get; set; }

    [JsonPropertyName("net_margin_used")]
    public decimal? NetMarginUsed { get; set; }

    [JsonPropertyName("brokerage_and_charges")]
    public decimal? BrokerageAndCharges { get; set; }

    [JsonPropertyName("adhoc_margin")]
    public decimal? AdhocMargin { get; set; }
}
