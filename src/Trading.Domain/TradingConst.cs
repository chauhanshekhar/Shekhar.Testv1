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

    // True for signals whose OptionSL tracks a single instrument's LTP (SINGLE/HERO_ZERO) —
    // eligible for a broker-managed SL_M stop order. False for BULL_SPREAD/BEAR_SPREAD, whose
    // OptionSL is a net two-leg premium (PositionTrigger: leg1Ltp - leg2Ltp) and can't be
    // expressed as a single-instrument broker order.
    public static bool IsSingleLegStrategy(string strategyType) =>
        strategyType == StrategyType.Single || strategyType == StrategyType.HeroZero;


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
        public const string PcrFullChain = "PCR_FULL_CHAIN";
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
        // OptionSL trailed ABOVE the entry premium: the exit locked a profit, it was not a loss-making stop-out.
        public const string TrailingSlHit = "TRAILING_SL_HIT";
        public const string OptionT2Hit   = "OPTION_T2_HIT";
        public const string OptionT1Hit   = "PARTIAL_T1";
        public const string PartialT2     = "PARTIAL_T2";      // EnableT2Partial: T2 partial sale (runner keeps trailing)
        public const string PartialT3     = "PARTIAL_T3";      // target ladder: middle-target partial sale
        public const string OptionT5Hit   = "OPTION_T5_HIT";   // target ladder: final target, full exit
        public const string IndexSlHit    = "INDEX_SL_HIT";
        public const string ForceExitEod  = "FORCE_EXIT_EOD";
        public const string HeroZero      = "HERO_ZERO";

        /// <summary>
        /// Label for an exit at the option stop: <see cref="TrailingSlHit"/> when the stop had been trailed above the entry
        /// premium (the trade exits in profit), otherwise <see cref="OptionSlHit"/>. Label only - the exit itself is unchanged.
        /// </summary>
        public static string ForOptionStop(decimal optionSl, decimal entryPremium)
            => entryPremium > 0 && optionSl > entryPremium ? TrailingSlHit : OptionSlHit;
    }
}
