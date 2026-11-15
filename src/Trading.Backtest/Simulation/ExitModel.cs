using Trading.Backtest.Settings;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Backtest.Simulation;

public enum StopModel
{
    /// <summary>The option stop never moves (arm and trailing do not raise it).</summary>
    Fixed,
    /// <summary>What the code does: breakeven arm, OptionSL to entry after a partial, ATR trailing, step-lock, index-T1 lock.</summary>
    Live,
}

/// <summary>Per-symbol exit parameters, resolved from the settings the way ExitDecisionService reads them.</summary>
public sealed record SymbolExitSettings(
    bool Ladder, bool ArmOn, bool PartialSaleAtArm, bool StepLock,
    double Buffer, double MidR, double FinalR,
    double Delta, double EntryPremium, int LotSize, bool T2Partial = false);

/// <summary>
/// Everything the exit replay needs. Stop geometry follows AtrRiskService: 1R in index points =
/// clamp(ATR x AtrStopLossMultiplier, MinimumIndexStopPoints, MaximumIndexStopPoints); T1 and T2 are
/// AtrTarget1Multiplier / AtrTarget2Multiplier divided by the stop multiplier, in R.
/// </summary>
public sealed record ExitModel(
    double StopMult, double T1Mult, double T2Mult, double MinStop, double MaxStop, double TrailPct,
    int Lots, IReadOnlyDictionary<string, SymbolExitSettings> Symbols)
{
    public static readonly string[] SymbolNames = ["NIFTY", "BANKNIFTY", "SENSEX"];

    /// <param name="avgDelta">Average |DeltaAtEntry| per symbol from real signals (premium = index move x delta).</param>
    /// <param name="avgPremium">Average OptionLtp at entry per symbol from real signals.</param>
    /// <param name="lotSizes">Contracts per lot per symbol (TradingInstruments.LotSize).</param>
    public static ExitModel From(
        BacktestSettings s, TradingSettings t,
        IReadOnlyDictionary<string, double> avgDelta,
        IReadOnlyDictionary<string, double> avgPremium,
        IReadOnlyDictionary<string, int> lotSizes)
    {
        var lots = Math.Max(1, t.AdaptiveLotMin);
        var per = new Dictionary<string, SymbolExitSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var sym in SymbolNames)
        {
            per[sym] = new SymbolExitSettings(
                Ladder: s.GetBool("EnableTargetLadder", sym, false),
                ArmOn: s.GetBool("EnableBreakevenArm", sym, true),
                PartialSaleAtArm: s.GetBool("EnableBreakevenPartialSale", sym, true) && lots > 1,
                StepLock: s.GetBool("EnableStepLock", sym, false),
                Buffer: (double)s.GetDecimal("BreakevenBufferPoints", sym, 15m),
                MidR: (double)s.GetDecimal("TargetLadderMidR", sym, 3m),
                FinalR: (double)s.GetDecimal("TargetLadderFinalR", sym, 5m),
                Delta: avgDelta.TryGetValue(sym, out var d) && d > 0 ? d : 0.45,
                EntryPremium: avgPremium.TryGetValue(sym, out var p) && p > 0 ? p : 100.0,
                LotSize: lotSizes.TryGetValue(sym, out var l) && l > 0 ? l : 1,
                // Live ignores EnableT2Partial while the ladder is on, and only acts on 3+ lots.
                T2Partial: s.GetBool("EnableT2Partial", sym, false) && lots > 2);
        }

        return new ExitModel(
            (double)t.AtrStopLossMultiplier, (double)t.AtrTarget1Multiplier, (double)t.AtrTarget2Multiplier,
            (double)t.MinimumIndexStopPoints, (double)t.MaximumIndexStopPoints, (double)t.TrailSLActivationPct,
            lots, per);
    }

    /// <summary>1R in index points for an ATR.</summary>
    public double IndexStopPoints(double atr) => Math.Min(Math.Max(atr * StopMult, MinStop), MaxStop);
}

/// <summary>Optional per-order cost; when absent every "after costs" figure is NULL, never silently equal to gross.</summary>
public sealed record CostModel(double PerOrderRs, double SlippagePointsPerSide = 0)
{
    /// <summary>Each order pays the fixed charge plus slippage on the full position quantity (an upper bound for partial orders).</summary>
    public double Cost(int orders, int lotSize, int lots) =>
        orders * (PerOrderRs + SlippagePointsPerSide * lotSize * lots);
}
