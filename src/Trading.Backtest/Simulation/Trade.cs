using Trading.Backtest.Entry;

namespace Trading.Backtest.Simulation;

/// <summary>Where the premium path of a trade came from; every figure in a report carries it.</summary>
public enum PremiumSource
{
    /// <summary>Index move x the symbol's average entry delta (no option price history exists for that day).</summary>
    IndexDeltaModel,
    /// <summary>Stored OptionMinuteHistory LTP of the ATM contract (from 2026-10-07 only).</summary>
    OptionMinuteHistory,
}

/// <summary>One simulated position (partial exits are folded into a single trade, like the journal aggregated by SignalId).</summary>
public sealed record Trade(
    string Symbol, TradeSide Side, DateTime EntryUtc, DateTime ExitUtc, string EntryPath,
    PremiumSource Source, double EntryPremium, double R,
    double PnlRs, double? CostRs, string ExitReason, int Orders,
    bool ReachedT1, bool ReachedMid, bool ReachedFinal,
    /// <summary>
    /// What the indicators read at the entry minute. Carried so a run can be bucketed by the
    /// conditions that produced each trade - in particular on a run with the RSI filter OFF, which
    /// is the only way to see the base rate across RSI values rather than the band's own selection.
    /// </summary>
    EntryContext? Entry = null)
{
    public DateOnly TradeDate => DateOnly.FromDateTime(EntryUtc + Data.SessionClock.IstOffset);

    /// <summary>Net of modelled costs, or null when no cost model was supplied.</summary>
    public double? NetRs => CostRs is { } c ? PnlRs - c : null;
}

/// <summary>Indicator values at the entry minute, all known at that moment.</summary>
public sealed record EntryContext(
    double Rsi1m, double? Rsi5m, double? Pcr, double AtrPct,
    double? OpenGapPct, double DayChangePct, double StretchAtr, double? Efficiency);
