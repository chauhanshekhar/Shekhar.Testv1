using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class HoldingEntry
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("isin")]
    public string Isin { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("average_price")]
    public decimal? AveragePrice { get; set; }

    [JsonPropertyName("t1_quantity")]
    public int? T1Quantity { get; set; }

    [JsonPropertyName("pledge_quantity")]
    public int? PledgeQuantity { get; set; }

    [JsonPropertyName("demat_locked_quantity")]
    public int? DematLockedQuantity { get; set; }

    [JsonPropertyName("groww_locked_quantity")]
    public int? GrowwLockedQuantity { get; set; }

    [JsonPropertyName("repledge_quantity")]
    public int? RepledgeQuantity { get; set; }

    [JsonPropertyName("demat_free_quantity")]
    public int? DematFreeQuantity { get; set; }

    [JsonPropertyName("corporate_action_additional_quantity")]
    public int? CorporateActionAdditionalQuantity { get; set; }

    [JsonPropertyName("active_demat_transfer_quantity")]
    public int? ActiveDematTransferQuantity { get; set; }
}
