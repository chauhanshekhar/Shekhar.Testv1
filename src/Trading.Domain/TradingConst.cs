namespace Trading.Domain;

/// <summary>
/// Single source of truth for all domain string constants.
/// Use these everywhere instead of inline magic strings.
/// </summary>
public static class TradingConst
{
    public static class OptionType
    {
        public const string CE = "CE";
        public const string PE = "PE";
    }

    public static class SignalType
    {
        public const string CALL = "CALL";
        public const string PUT = "PUT";
    }

    public static class StrategyType
    {
        public const string Single      = "SINGLE";
        public const string BullSpread  = "BULL_SPREAD";
        public const string BearSpread  = "BEAR_SPREAD";
        public const string HeroZero    = "HERO_ZERO";
        public const string Auto        = "AUTO";
        public const string Spread      = "SPREAD";
    }


    public static class Exchange
    {
        public const string Nse = "NSE";
        public const string Bse = "BSE";
    }

    public static class Segment
    {
        public const string Fno = "FNO";
    }

    public static class Indicator
    {
        public const string PcrAtm = "PCR_ATM";
        public const string Ema20  = "EMA20";
        public const string Ema50  = "EMA50";
        public const string Rsi    = "RSI";
        public const string Atr    = "ATR";
    }

    public static class TradeStatus
    {
        public const string Open      = "OPEN";
        public const string Closed    = "CLOSED";
        public const string SlHit     = "SL_HIT";
        public const string TargetHit = "TARGET_HIT";
    }

    public static class ExitReason
    {
        public const string OptionSlHit   = "OPTION_SL_HIT";
        public const string OptionT2Hit   = "OPTION_T2_HIT";
        public const string OptionT1Hit   = "PARTIAL_T1";
        public const string IndexSlHit    = "INDEX_SL_HIT";
        public const string ForceExitEod  = "FORCE_EXIT_EOD";
        public const string HeroZero      = "HERO_ZERO";
    }
}
