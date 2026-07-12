using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Single source of truth for entry/current combined position value.
/// Used consistently across RiskGate, RiskRewardGate, and ExitWorker
/// so spread risk, reward, and exit triggers are all on the same scale.
/// </summary>
public static class PositionTrigger
{
    /// <summary>
    /// Entry-time combined value:
    ///   Spread → netDebit = longLtp - shortLtp
    ///   Single → optionLtp
    /// </summary>
    public static decimal EntryValue(Signal s) => s.StrategyType switch
    {
        TradingConst.StrategyType.BullSpread or TradingConst.StrategyType.BearSpread
            => s.Leg2Ltp > 0 ? s.OptionLtp - s.Leg2Ltp : s.OptionLtp,
        _ => s.OptionLtp
    };

    /// <summary>
    /// Current combined value from live chain LTPs.
    ///   Spread → longLtp - shortLtp
    ///   Single → leg1Ltp
    /// </summary>
    public static decimal CurrentValue(Signal s, decimal leg1Ltp, decimal leg2Ltp) =>
        s.StrategyType switch
        {
            TradingConst.StrategyType.BullSpread or TradingConst.StrategyType.BearSpread
                => leg2Ltp > 0 ? leg1Ltp - leg2Ltp : leg1Ltp,
            _ => leg1Ltp
        };
}
