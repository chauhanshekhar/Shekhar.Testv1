using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class TradeDetail
{
    [JsonPropertyName("groww_trade_id")]
    public string GrowwTradeId { get; set; } = "";

    [JsonPropertyName("groww_order_id")]
    public string GrowwOrderId { get; set; } = "";

    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("trade_status")]
    public string TradeStatus { get; set; } = "";

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = "";

    [JsonPropertyName("segment")]
    public string Segment { get; set; } = "";

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = "";

    [JsonPropertyName("product")]
    public string Product { get; set; } = "";

    [JsonPropertyName("isin")]
    public string? Isin { get; set; }

    [JsonPropertyName("exchange_trade_id")]
    public string? ExchangeTradeId { get; set; }

    [JsonPropertyName("exchange_order_id")]
    public string? ExchangeOrderId { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("trade_date_time")]
    public string? TradeDateTime { get; set; }

    [JsonPropertyName("settlement_number")]
    public string? SettlementNumber { get; set; }
}
