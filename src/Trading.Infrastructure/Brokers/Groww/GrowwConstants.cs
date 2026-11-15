namespace Trading.Infrastructure.Brokers.Groww;

public static class GrowwExchange
{
    public const string Nse = "NSE";
    public const string Bse = "BSE";
    public const string Mcx = "MCX";
}

public static class GrowwSegment
{
    public const string Cash = "CASH";
    public const string Fno = "FNO";
    public const string Commodity = "COMMODITY";
}

public static class GrowwInterval
{
    public const string OneMinute = "1min";
    public const string TwoMinute = "2min";
    public const string ThreeMinute = "3min";
    public const string FiveMinute = "5min";
    public const string TenMinute = "10min";
    public const string FifteenMinute = "15min";
    public const string ThirtyMinute = "30min";
    public const string OneHour = "1hour";
    public const string FourHour = "4hour";
    public const string OneDay = "1day";
    public const string OneWeek = "1week";
    public const string OneMonth = "1month";

    /// <summary>
    /// Minutes for the historical endpoint's <c>interval_in_minutes</c> parameter. Throws for an
    /// interval it cannot express rather than let Groww silently fall back to 1-minute candles —
    /// which is what happened while these strings were sent as <c>candle_interval</c> (an
    /// unknown parameter on /v1/historical/candle/range): every "SYMBOL_5m" candle stored until
    /// 2026-09-30 is really a 1-minute candle.
    /// </summary>
    public static int ToMinutes(string interval) => interval switch
    {
        OneMinute => 1,
        TwoMinute => 2,
        ThreeMinute => 3,
        FiveMinute => 5,
        TenMinute => 10,
        FifteenMinute => 15,
        ThirtyMinute => 30,
        OneHour => 60,
        FourHour => 240,
        OneDay => 1440,
        OneWeek => 10080,
        _ when int.TryParse(interval, out var minutes) && minutes > 0 => minutes,
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval,
                 "No interval_in_minutes equivalent for this candle interval"),
    };
}

public static class GrowwProduct
{
    public const string Cnc = "CNC";
    public const string Mis = "MIS";
    public const string Nrml = "NRML";
}

public static class GrowwOrderType
{
    public const string Market = "MARKET";
    public const string Limit = "LIMIT";
    public const string Sl = "SL";
    public const string SlM = "SL_M";
}

public static class GrowwTransactionType
{
    public const string Buy = "BUY";
    public const string Sell = "SELL";
}

public static class GrowwValidity
{
    public const string Day = "DAY";
    public const string Ioc = "IOC";
}

public static class GrowwSmartOrderType
{
    public const string Gtt = "GTT";
    public const string Oco = "OCO";
}

// All Groww REST API endpoint paths — single source of truth.
// Base URL comes from GrowwSettings.BaseUrl (appsettings.json → Groww:BaseUrl).
public static class GrowwEndpoint
{
    // Orders
    public const string OrderCreate = "/v1/order/create";
    public const string OrderCancel = "/v1/order/cancel";
    public const string OrderModify = "/v1/order/modify";
    public const string OrderList = "/v1/order/list";
    public const string OrderDetail = "/v1/order/detail/{0}";          // {0} = growwOrderId
    public const string OrderByReference = "/v1/order/status/reference/{0}";// {0} = referenceId
    public const string OrderTrades = "/v1/order/trades/{0}";          // {0} = growwOrderId

    // Smart Orders (GTT / OCO)
    public const string SmartOrderCreate = "/v1/order-advance/create";
    public const string SmartOrderModify = "/v1/order-advance/modify/{0}";  // {0} = smartOrderId
    public const string SmartOrderCancel = "/v1/order-advance/cancel/{0}/{1}/{2}"; // segment/type/id
    public const string SmartOrderStatus = "/v1/order-advance/status/{0}/{1}/internal/{2}";
    public const string SmartOrderList = "/v1/order-advance/list";

    // Portfolio
    public const string Holdings = "/v1/holdings/user";
    public const string Positions = "/v1/positions/user";
    public const string PositionBySymbol = "/v1/positions/trading-symbol";

    // Margins
    public const string MarginUser = "/v1/margins/detail/user";
    public const string MarginOrder = "/v1/margins/detail/orders";

    // Live Market Data
    public const string LiveQuote = "/v1/live-data/quote";
    public const string LiveLtp = "/v1/live-data/ltp";
    public const string LiveOhlc = "/v1/live-data/ohlc";
    public const string LiveGreeks = "/v1/live-data/greeks";
    public const string OptionChain = "/v1/option-chain/exchange/{0}"; // {0} = exchange

    // Historical Data
    public const string HistoricalCandles = "/v1/historical/candle/range";
    public const string HistoricalExpiries = "/v1/historical/expiries";
    public const string HistoricalContracts = "/v1/historical/contracts";

    // User
    public const string UserDetail = "/v1/user/detail";
}


