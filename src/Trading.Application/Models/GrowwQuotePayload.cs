using System.Text.Json.Serialization;

namespace Trading.Application.Models;

public class GrowwQuotePayload
{
    [JsonPropertyName("trading_symbol")]
    public string TradingSymbol { get; set; } = "";


    [JsonPropertyName("last_price")]
    public decimal? Ltp { get; set; }


    [JsonPropertyName("average_price")]
    public decimal? AveragePrice { get; set; }


    [JsonPropertyName("ohlc")]
    public GrowwQuoteOhlc? Ohlc { get; set; }


    [JsonPropertyName("depth")]
    public GrowwDepth? Depth { get; set; }


    [JsonPropertyName("bid_price")]
    public decimal? BidPrice { get; set; }


    [JsonPropertyName("bid_quantity")]
    public long? BidQuantity { get; set; }


    [JsonPropertyName("offer_price")]
    public decimal? OfferPrice { get; set; }


    [JsonPropertyName("offer_quantity")]
    public long? OfferQuantity { get; set; }


    [JsonPropertyName("day_change")]
    public decimal? DayChange { get; set; }


    [JsonPropertyName("day_change_perc")]
    public decimal? DayChangePerc { get; set; }


    [JsonPropertyName("volume")]
    public long? Volume { get; set; }


    [JsonPropertyName("upper_circuit_limit")]
    public decimal? UpperCircuitLimit { get; set; }


    [JsonPropertyName("lower_circuit_limit")]
    public decimal? LowerCircuitLimit { get; set; }


    [JsonPropertyName("open_interest")]
    public decimal? OpenInterest { get; set; }


    [JsonPropertyName("previous_open_interest")]
    public decimal? PreviousOpenInterest { get; set; }


    [JsonPropertyName("oi_day_change")]
    public decimal? OiDayChange { get; set; }


    [JsonPropertyName("oi_day_change_percentage")]
    public decimal? OiDayChangePercentage { get; set; }


    [JsonPropertyName("total_buy_quantity")]
    public long? TotalBuyQuantity { get; set; }


    [JsonPropertyName("total_sell_quantity")]
    public long? TotalSellQuantity { get; set; }


    [JsonPropertyName("week_52_high")]
    public decimal? Week52High { get; set; }


    [JsonPropertyName("week_52_low")]
    public decimal? Week52Low { get; set; }


    [JsonPropertyName("implied_volatility")]
    public decimal? ImpliedVolatility { get; set; }


    [JsonPropertyName("market_cap")]
    public decimal? MarketCap { get; set; }


    [JsonPropertyName("last_trade_quantity")]
    public long? LastTradeQuantity { get; set; }


    [JsonPropertyName("last_trade_time")]
    public long? LastTradeTime { get; set; }


    [JsonPropertyName("high_trade_range")]
    public decimal? HighTradeRange { get; set; }


    [JsonPropertyName("low_trade_range")]
    public decimal? LowTradeRange { get; set; }
}


public class GrowwQuoteOhlc
{
    [JsonPropertyName("open")]
    public decimal? Open { get; set; }


    [JsonPropertyName("high")]
    public decimal? High { get; set; }


    [JsonPropertyName("low")]
    public decimal? Low { get; set; }


    [JsonPropertyName("close")]
    public decimal? Close { get; set; }
}


public class GrowwDepth
{
    [JsonPropertyName("buy")]
    public List<GrowwDepthItem> Buy { get; set; } = new();


    [JsonPropertyName("sell")]
    public List<GrowwDepthItem> Sell { get; set; } = new();
}


public class GrowwDepthItem
{
    [JsonPropertyName("price")]
    public decimal? Price { get; set; }


    [JsonPropertyName("quantity")]
    public long? Quantity { get; set; }


    [JsonPropertyName("orderCount")]
    public int? OrderCount { get; set; }
}
