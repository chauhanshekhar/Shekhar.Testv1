using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class PositionEntry
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "";

    [JsonPropertyName("product")]
    public string Product { get; set; } = "";

    [JsonPropertyName("symbol_isin")]
    public string? SymbolIsin { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("credit_quantity")]
    public int? CreditQuantity { get; set; }

    [JsonPropertyName("credit_price")]
    public decimal? CreditPrice { get; set; }

    [JsonPropertyName("debit_quantity")]
    public int? DebitQuantity { get; set; }

    [JsonPropertyName("debit_price")]
    public decimal? DebitPrice { get; set; }

    [JsonPropertyName("carry_forward_credit_quantity")]
    public int? CarryForwardCreditQuantity { get; set; }

    [JsonPropertyName("carry_forward_credit_price")]
    public decimal? CarryForwardCreditPrice { get; set; }

    [JsonPropertyName("carry_forward_debit_quantity")]
    public int? CarryForwardDebitQuantity { get; set; }

    [JsonPropertyName("carry_forward_debit_price")]
    public decimal? CarryForwardDebitPrice { get; set; }

    [JsonPropertyName("net_carry_forward_quantity")]
    public int? NetCarryForwardQuantity { get; set; }

    [JsonPropertyName("net_price")]
    public decimal? NetPrice { get; set; }

    [JsonPropertyName("net_carry_forward_price")]
    public decimal? NetCarryForwardPrice { get; set; }

    [JsonPropertyName("realised_pnl")]
    public decimal? RealisedPnl { get; set; }
}
