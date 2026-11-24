using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;

namespace Trading.Backtest.Configurations;

public enum SettingChangeKind { Set, Remove, Deactivate }

/// <summary>One edit to the live settings snapshot. Symbol null = the global row.</summary>
public sealed record SettingChange(string Key, string? Symbol, SettingChangeKind Kind, decimal Value = 0m)
{
    public static SettingChange Set(string key, decimal value, string? symbol = null) => new(key, symbol, SettingChangeKind.Set, value);
    public static SettingChange Off(string key, string? symbol = null) => new(key, symbol, SettingChangeKind.Deactivate);
}

/// <summary>
/// A named, independently configurable experiment: a side (CALL or PUT, never mixed) and the edits applied to a copy of the
/// live settings. It never writes anywhere; the live snapshot passed to <see cref="Apply"/> is left untouched.
/// </summary>
/// <summary>How many positions a configuration may hold at once, and whether the cap is per symbol.</summary>
public sealed record SlotRules(int MaxConcurrent = 1, bool PerSymbol = false, bool NoHandoffCooldown = false, bool SharedBothSides = false);

/// <summary>Control-arm sampling: keep only every Nth valid entry (no market information).</summary>
public sealed record SamplingRules(int KeepEveryNth = 1, int? MaxTradesPerSymbolDay = null);

/// <summary>
/// Research: override the ATR stop / target multipliers the exit replay uses (null field = keep the live value). Entries are unaffected,
/// including the index-stop floor gate, so arms that differ only by this record trade the same signals.
/// </summary>
public sealed record ExitGeometry(double? StopMult = null, double? T1Mult = null, double? T2Mult = null)
{
    public ExitModel ApplyTo(ExitModel live)
    {
        var model = live with { StopMult = StopMult ?? live.StopMult, T1Mult = T1Mult ?? live.T1Mult, T2Mult = T2Mult ?? live.T2Mult };
        // An explicit T2 multiplier in a research arm replaces the Target2RMultiple row (the row would otherwise win in ExitReplay).
        return T2Mult is null
            ? model
            : model with { Symbols = model.Symbols.ToDictionary(kv => kv.Key, kv => kv.Value with { Target2R = null }, StringComparer.OrdinalIgnoreCase) };
    }
}

public sealed record BacktestConfiguration(string Name, TradeSide Side, string Description, IReadOnlyList<SettingChange> Changes, EntryRules? Rules = null, SlotRules? Slots = null, SamplingRules? Sampling = null, ExitGeometry? Exit = null)
{
    public string TradeType => Slots?.SharedBothSides == true ? "BOTH" : Side == TradeSide.Call ? "CALL" : "PUT";

    public BacktestSettings Apply(BacktestSettings live)
    {
        var copy = live.Clone();
        foreach (var c in Changes)
        {
            switch (c.Kind)
            {
                case SettingChangeKind.Set: copy.Set(c.Key, c.Symbol, c.Value); break;
                case SettingChangeKind.Remove: copy.Remove(c.Key, c.Symbol); break;
                case SettingChangeKind.Deactivate: copy.Deactivate(c.Key, c.Symbol); break;
            }
        }
        return copy;
    }
}

/// <summary>Ranges for the combination grid. Every list is configurable; an empty list drops that dimension.</summary>
public sealed record GridOptions
{
    public decimal[] CallRsiFloors { get; init; } = [40m, 45m, 50m, 55m];
    public decimal[] CallRsiCeilings { get; init; } = [60m, 65m, 70m, 75m];
    public decimal[] PutRsiFloors { get; init; } = [25m, 28m, 30m, 32m];
    public decimal[] PutRsiCeilings { get; init; } = [35m, 38m, 40m, 42m];
    /// <summary>EMA gap minimum for NIFTY and SENSEX in index points; BANKNIFTY uses <see cref="BankNiftyGapRatio"/> times this.</summary>
    public decimal[] EmaGapMinPoints { get; init; } = [5m, 10m, 15m, 20m];
    public decimal BankNiftyGapRatio { get; init; } = 2.75m;
    public decimal[] CallPcrMinimums { get; init; } = [0.7m, 0.8m, 0.9m, 1.0m];
    public decimal[] PutPcrMinimums { get; init; } = [0.6m, 0.7m, 0.8m, 0.9m];
    public decimal[] AtrPercentMinimums { get; init; } = [0.02m, 0.03m, 0.04m];
    /// <summary>Session-efficiency thresholds swept by BuildEfficiencySweep.</summary>
    public decimal[] EfficiencyThresholds { get; init; } = [0.25m, 0.35m, 0.50m, 0.65m];

    // ── stretch sweep (BuildStretchSweep) ──────────────────────────────────────────────────────────
    /// <summary>Maximum ATRs beyond EMA20 allowed at entry. The live rules have no cap.</summary>
    public decimal[] StretchMaxAtr { get; init; } = [0.50m, 0.75m, 1.00m, 1.25m, 1.50m, 2.00m];
    /// <summary>Contrast arm: take only entries at least this stretched.</summary>
    public decimal[] StretchMinAtr { get; init; } = [1.50m];

    // ── opening-type sweep (BuildOpeningTypes) ────────────────────────────────────────────────────────────────────
    /// <summary>Gap size in percent separating GapUp / Normal / GapDown for the full sweep.</summary>
    public double OpeningGapThresholdPct { get; init; } = 0.30;
    /// <summary>Extra boundaries re-run on the group baseline only, to show whether the boundary drives the result.</summary>
    public double[] OpeningGapSensitivityPct { get; init; } = [0.20, 0.50];
    /// <summary>EMA gap minimums swept inside each group: the live value and 1.5x it (BANKNIFTY scales by BankNiftyGapRatio).</summary>
    public decimal[] OpeningEmaGapPoints { get; init; } = [10m, 15m];
    public decimal[] OpeningAtrPercentMinimums { get; init; } = [0.03m, 0.04m, 0.05m];
    public decimal OpeningBodyPctMinimum { get; init; } = 35m;

    /// <summary>The full grid is capped so a typo cannot ask for millions of runs.</summary>
    public int MaxGridConfigurations { get; init; } = 600;
}

/// <summary>
/// Builds the named experiments: baseline, each filter removed, each threshold varied one at a time, pairwise removals,
/// RSI x PCR, and a configurable full grid. CALL and PUT are always separate lists.
/// </summary>
public static class ConfigurationCatalog
{
    private static readonly string[] Symbols = ["NIFTY", "BANKNIFTY", "SENSEX"];

    public static IReadOnlyList<BacktestConfiguration> Build(TradeSide side, GridOptions g, bool includeFullGrid = true)
    {
        var list = new List<BacktestConfiguration>();
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        BacktestConfiguration Make(string name, string desc, params SettingChange[][] parts) =>
            new($"{tag}|{name}", side, desc, parts.SelectMany(p => p).ToList());

        list.Add(Make("Baseline", "current live settings", []));

        // 2. one filter removed
        list.Add(Make("NoRsi", "RSI band removed", NoRsi(side)));
        list.Add(Make("NoEmaGap", "EMA-gap filter removed", NoGap()));
        list.Add(Make("NoPcr", "PCR filter removed (PUT: only SENSEX has a PCR rule)", NoPcr(side)));
        list.Add(Make("NoAtr", "ATR% gate removed", NoAtr()));

        // gates that decide whether a CALL may enter after the 5m trend has turned (CALL only: both gates are CALL-only in the live settings)
        if (side == TradeSide.Call)
        {
            list.Add(Make("NoEarlyTrendGate", "early-trend gate off: CALL allowed after the 5m trend is already up", [SettingChange.Set("EnableEarlyTrendGate", 0m)]));
            list.Add(Make("NoNoTradeWindow", "12:00-12:30 no-trade window off", [SettingChange.Set("EnableNoTradeWindowGate", 0m)]));
            foreach (var c in g.CallRsiCeilings)
                list.Add(Make($"RsiCeiling={c}+NoEarlyTrendGate", $"RSI upper bound {c} with the early-trend gate off",
                    [RsiCeilingChange(side, c)], [SettingChange.Set("EnableEarlyTrendGate", 0m)]));
        }

        // 3 + 4. RSI bound variations (one bound at a time)
        foreach (var f in RsiFloors(side, g))
            list.Add(Make($"RsiFloor={f}", $"RSI lower bound {f}", [RsiFloorChange(side, f)]));
        foreach (var c in RsiCeilings(side, g))
            list.Add(Make($"RsiCeiling={c}", $"RSI upper bound {c}", [RsiCeilingChange(side, c)]));

        // gap, PCR, ATR threshold variations
        foreach (var gap in g.EmaGapMinPoints)
            list.Add(Make($"EmaGap={gap}", $"EMA gap minimum {gap} points (BANKNIFTY x{g.BankNiftyGapRatio})", Gap(gap, g)));
        foreach (var pcr in PcrMinimums(side, g))
            list.Add(Make($"PcrMin={pcr}", $"PCR minimum {pcr}", Pcr(side, pcr)));
        foreach (var atr in g.AtrPercentMinimums)
            list.Add(Make($"AtrMin={atr}", $"ATR% minimum {atr}", Atr(atr)));

        // 9. pairwise removals of the four filters
        var removals = new (string Name, SettingChange[] Changes)[]
        {
            ("Rsi", NoRsi(side)), ("EmaGap", NoGap()), ("Pcr", NoPcr(side)), ("Atr", NoAtr()),
        };
        for (var i = 0; i < removals.Length; i++)
            for (var j = i + 1; j < removals.Length; j++)
                list.Add(Make($"No{removals[i].Name}+No{removals[j].Name}", "two filters removed", removals[i].Changes, removals[j].Changes));

        // 8. combined RSI and PCR variation
        foreach (var f in RsiFloors(side, g))
            foreach (var c in RsiCeilings(side, g))
                foreach (var pcr in PcrMinimums(side, g))
                    list.Add(Make($"Rsi={f}-{c}+Pcr={pcr}", "RSI band and PCR minimum varied together",
                        [RsiFloorChange(side, f), RsiCeilingChange(side, c)], Pcr(side, pcr)));

        // 10. full grid: RSI floor x ceiling x gap x PCR x ATR
        if (includeFullGrid)
        {
            var count = 0;
            foreach (var f in RsiFloors(side, g))
            foreach (var c in RsiCeilings(side, g))
            foreach (var gap in g.EmaGapMinPoints)
            foreach (var pcr in PcrMinimums(side, g))
            foreach (var atr in g.AtrPercentMinimums)
            {
                if (f >= c) continue;
                if (++count > g.MaxGridConfigurations) break;
                list.Add(Make($"Grid|Rsi={f}-{c}|Gap={gap}|Pcr={pcr}|Atr={atr}", "full combination grid",
                    [RsiFloorChange(side, f), RsiCeilingChange(side, c)], Gap(gap, g), Pcr(side, pcr), Atr(atr)));
            }
        }

        return list.GroupBy(c => c.Name).Select(x => x.First()).ToList();
    }

    /// <summary>
    /// The experiments for the "gap up and keeps rising" question, each alone and combined, with and without the PCR condition
    /// (PCR exists only on snapshot days, so the live PCR rule leaves most history untested). Requires SelectorMode.Replay to mean anything.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildRelaxations(TradeSide side, GridOptions g)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var call = side == TradeSide.Call;
        var list = new List<BacktestConfiguration>();
        var ext = call ? 80m : 20m;                       // wider RSI edge: CALL ceiling 80 / PUT floor 20
        var gapExt = call ? 85m : 15m;                    // while a gap trend is active

        foreach (var noPcr in new[] { false, true })
        {
            var prefix = noPcr ? "NoPcr+" : "";
            var baseChanges = noPcr ? NoPcr(side) : [];

            void Add(string name, string desc, EntryRules? rules, params SettingChange[][] parts) =>
                list.Add(new BacktestConfiguration($"{tag}|{prefix}{name}", side, desc,
                    baseChanges.Concat(parts.SelectMany(p => p)).ToList(), rules));

            Add("Baseline", "live rules with the selector replayed", null);
            Add("Sideways5mAgrees", "Sideways regime does not suppress when the 5m trend agrees", new EntryRules { Sideways = SidewaysOverride.When5mAgrees });
            Add("SidewaysOff", "Sideways regime never suppresses", new EntryRules { Sideways = SidewaysOverride.Always });
            Add("Gap5pts", "EMA gap minimum 5 points (BANKNIFTY x2.75)", null, Gap(5m, g));
            Add("NoEmaGap", "EMA-gap rule removed", null, NoGap());
            Add(call ? "RsiCeiling80" : "RsiFloor20", "wider RSI edge", null, [call ? RsiCeilingChange(side, ext) : RsiFloorChange(side, ext)]);
            if (call) Add("NoEarlyTrendGate", "early-trend gate off", null, [SettingChange.Set("EnableEarlyTrendGate", 0m)]);

            // the gap-trend continuation rule, each part alone, then all together
            if (call)
                Add("GapTrend:RsiOnly", "gap that keeps going: wider RSI only", new EntryRules { GapTrendContinuation = true, GapTrendCallRsiCeiling = gapExt });
            Add("GapTrend:SidewaysOnly", "gap that keeps going: no Sideways suppression only", new EntryRules { GapTrendContinuation = true, GapTrendSkipsSidewaysSuppression = true });
            if (call) Add("GapTrend:EarlyTrendOnly", "gap that keeps going: early-trend gate skipped only", new EntryRules { GapTrendContinuation = true, GapTrendSkipsEarlyTrendGate = true });
            Add("GapTrend:EmaGapOnly", "gap that keeps going: EMA-gap rule waived only", new EntryRules { GapTrendContinuation = true, GapTrendWaivesEmaGapFilter = true });
            Add("GapTrend:All", "gap that keeps going: wider RSI, no Sideways suppression, early-trend skipped, EMA-gap waived",
                new EntryRules
                {
                    GapTrendContinuation = true,
                    GapTrendCallRsiCeiling = call ? gapExt : null,
                    GapTrendSkipsSidewaysSuppression = true,
                    GapTrendSkipsEarlyTrendGate = call,
                    GapTrendWaivesEmaGapFilter = true,
                });
            Add("Combined:Sideways5m+Gap5+Rsi", "5m-agrees Sideways override, EMA gap 5, wider RSI", new EntryRules { Sideways = SidewaysOverride.When5mAgrees },
                Gap(5m, g), [call ? RsiCeilingChange(side, ext) : RsiFloorChange(side, ext)]);
        }
        return list;
    }

    /// <summary>
    /// The pasted gap-up continuation proposal: G1 = gap-up mode with its own path (no Sideways suppression, early-trend gate skipped) and
    /// the existing CALL rules; G2 = G1 + pullback with a bullish bar; G3 = G1 + confirmed breakout + exhaustion filter. Variants add the
    /// exhaustion filter to G2 and a 45-70 RSI band. For PUT: the counter-trend PUT block during gap-up mode.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildGapUpProposal(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>();

        foreach (var noPcr in new[] { false, true })
        {
            var prefix = noPcr ? "NoPcr+" : "";
            var baseChanges = noPcr ? NoPcr(side) : [];

            void Add(string name, string desc, EntryRules? rules, params SettingChange[] extra) =>
                list.Add(new BacktestConfiguration($"{tag}|{prefix}{name}", side, desc, baseChanges.Concat(extra).ToList(), rules));

            Add("Baseline", "live rules with the selector replayed", null);

            if (side == TradeSide.Call)
            {
                EntryRules G(bool pullback = false, bool breakout = false, double? exhaustion = null, decimal? rsiCeiling = null) => new()
                {
                    GapTrendContinuation = true, GapMinPct = 0.30,
                    GapTrendSkipsSidewaysSuppression = true, GapTrendSkipsEarlyTrendGate = true,
                    GapTrendCallRsiCeiling = rsiCeiling,
                    GapUpPullback = pullback, GapUpBreakout = breakout, GapUpExhaustionAtr = exhaustion,
                };
                Add("G1", "gap-up mode + existing CALL rules", G());
                Add("G1:Exhaustion", "G1 + exhaustion filter (1.5 ATR above EMA20)", G(exhaustion: 1.5));
                Add("G2", "G1 + pullback toward EMA20 with a bullish bar", G(pullback: true));
                Add("G2:Exhaustion", "G2 + exhaustion filter", G(pullback: true, exhaustion: 1.5));
                Add("G3", "G1 + confirmed breakout + exhaustion filter", G(breakout: true, exhaustion: 1.5));
                Add("G3:NoExhaustion", "G1 + confirmed breakout only", G(breakout: true));
                Add("G1:Rsi70", "G1 with the 5m RSI band widened to 45-70", G(rsiCeiling: 70m));
                Add("G2:Rsi70", "G2 with RSI 45-70", G(pullback: true, rsiCeiling: 70m));
                Add("G3:Rsi70", "G3 with RSI 45-70", G(breakout: true, exhaustion: 1.5, rsiCeiling: 70m));
            }
            else
            {
                Add("BlockPutInGapUp:Chain", "PUT chain blocked while the gap-up trend is active",
                    new EntryRules { GapTrendContinuation = true, GapUpBlocksPut = CounterTrendPut.BlockChain });
                Add("BlockPutInGapUp:ChainAndRule", "PUT chain and the BANKNIFTY breakdown rule blocked while the gap-up trend is active",
                    new EntryRules { GapTrendContinuation = true, GapUpBlocksPut = CounterTrendPut.BlockChainAndRule });
            }
        }
        return list;
    }

    /// <summary>
    /// The 2x2 on BANKNIFTY CALL: early-trend gate ON/OFF (a BANKNIFTY-only row) x RSI ceiling 65/70 (a BANKNIFTY-only row).
    /// A = baseline, B = gate off, C = ceiling 70, D = both. Each also without the PCR rule, which leaves history for most days untested.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildBankNiftyTwoByTwo()
    {
        var list = new List<BacktestConfiguration>();
        foreach (var noPcr in new[] { false, true })
        {
            var prefix = noPcr ? "NoPcr+" : "";
            var common = noPcr ? NoPcr(TradeSide.Call) : [];
            BacktestConfiguration Make(string name, string desc, params SettingChange[] extra) =>
                new($"CALL|{prefix}{name}", TradeSide.Call, desc, common.Concat(extra).ToList());

            var gateOff = SettingChange.Set("EnableEarlyTrendGate", 0m, "BANKNIFTY");
            var rsi70 = SettingChange.Set("CallRsiCeiling", 70m, "BANKNIFTY");
            list.Add(Make("A:Baseline(gate ON, RSI 45-65)", "live rules"));
            list.Add(Make("B:GateOff(RSI 45-65)", "BANKNIFTY early-trend gate off", gateOff));
            list.Add(Make("C:Rsi70(gate ON)", "BANKNIFTY CALL RSI ceiling 70", rsi70));
            list.Add(Make("D:Combined(gate OFF, RSI 45-70)", "both changes", gateOff, rsi70));
        }
        return list;
    }

    /// <summary>
    /// The two strategies for "gap up, RSI high, market keeps going up": MOMENTUM (gap-and-go breakout, RSI must be HIGH) and GAPHOLD
    /// (wait for the gap to hold, enter on a pullback), plus the same momentum entry without a gap for comparison, and the production baseline.
    /// CALL only; each has its own entry path, keeps the ATR / index-stop-floor / volatility gates, and takes at most 2 trades per symbol-day.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildTrendStrategies()
    {
        BacktestConfiguration Make(string name, string desc, TrendStrategyRules? t) =>
            new($"CALL|{name}", TradeSide.Call, desc, [], t is null ? null : new EntryRules { Trend = t });

        return
        [
            Make("Baseline", "production entry rules (selector replayed)", null),
            Make("S1:GapAndGoMomentum", "gap up >= 0.30%, above open/EMA20, 5m trend up, 5m RSI >= 60, bullish close above the 10-bar high, 09:20-14:00",
                new TrendStrategyRules { Strategy = TrendStrategy.Momentum, RequireGap = true, MinRsi5 = 60m }),
            Make("S1b:GapAndGoMomentum09:20-10:30", "S1 limited to the first hour and a quarter",
                new TrendStrategyRules { Strategy = TrendStrategy.Momentum, RequireGap = true, MinRsi5 = 60m, EndMinute = 10 * 60 + 30 }),
            Make("S2:GapHoldPullback", "gap up >= 0.30% that has not filled half, trend aligned, 5m RSI >= 50, pullback toward EMA20 with a bullish bar, 10:15-14:00",
                new TrendStrategyRules { Strategy = TrendStrategy.GapHold, RequireGap = true, MinRsi5 = 50m, StartMinute = 10 * 60 + 15 }),
            Make("S1n:MomentumNoGap", "same momentum entry on any day the index is up 0.3% from the open (no gap required)",
                new TrendStrategyRules { Strategy = TrendStrategy.Momentum, RequireGap = false, MinUpFromOpenPct = 0.30, MinRsi5 = 60m }),
            Make("S2n:HoldPullbackNoGap", "pullback entry on any day the index is up 0.3% from the open (no gap required)",
                new TrendStrategyRules { Strategy = TrendStrategy.GapHold, RequireGap = false, MinUpFromOpenPct = 0.30, MinRsi5 = 50m, StartMinute = 10 * 60 + 15 }),
        ];
    }

    /// <summary>
    /// Early-entry study (CALL): a dedicated entry within the first minutes of the 1m EMA20&gt;EMA50 turn, against the live rules.
    /// All arms use 5m RSI 45-70 (the live band), a bullish last bar and no per-day trade cap (the one-position rule still applies).
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildEarlyCross()
    {
        BacktestConfiguration Make(string name, string desc, TrendStrategyRules? t) =>
            new($"CALL|EC|{name}", TradeSide.Call, desc, [], t is null ? null : new EntryRules { Trend = t });
        TrendStrategyRules Rules(int bars, bool notUp = false, bool gap = false, bool selector = false) => new()
        {
            Strategy = TrendStrategy.EarlyCross, RequireGap = false, MinRsi5 = 45m, MaxRsi5 = 70m, MaxBarsSinceCross = bars,
            Require5mNotUp = notUp, RequireEmaGap = gap, ApplySelector = selector, MaxTradesPerSymbolDay = 99,
            StartMinute = 9 * 60 + 20, EndMinute = 15 * 60 + 15,
        };
        return
        [
            Make("Baseline", "production entry rules (selector replayed)", null),
            Make("Bars5", "within 5 min of the 1m cross, no selector", Rules(5)),
            Make("Bars10", "within 10 min of the 1m cross, no selector", Rules(10)),
            Make("Bars15", "within 15 min of the 1m cross, no selector", Rules(15)),
            Make("Bars10+Selector", "within 10 min, replayed selector applied", Rules(10, selector: true)),
            Make("Bars10+5mNotUp", "within 10 min, only while the 5m trend has not turned up", Rules(10, notUp: true)),
            Make("Bars10+EmaGap", "within 10 min, live EMA-gap rule kept", Rules(10, gap: true)),
            Make("Bars10+5mNotUp+EmaGap", "within 10 min, 5m not up, live EMA-gap rule kept", Rules(10, notUp: true, gap: true)),
        ];
    }

    /// <summary>PUT with the early-trend gate applied to PUT as well (EarlyTrendGateSide 0 = CALL and PUT), against the live PUT rules.</summary>
    public static IReadOnlyList<BacktestConfiguration> BuildPutEarlyTrend()
    {
        var list = new List<BacktestConfiguration>();
        foreach (var noPcr in new[] { false, true })
        {
            var prefix = noPcr ? "NoPcr+" : "";
            var common = noPcr ? NoPcr(TradeSide.Put) : [];
            list.Add(new BacktestConfiguration($"PUT|{prefix}Baseline(gate CALL only)", TradeSide.Put, "live PUT rules", common.ToList()));
            list.Add(new BacktestConfiguration($"PUT|{prefix}GateOnForPut(side 0)", TradeSide.Put, "early-trend gate also blocks a PUT once the 5m trend is already down",
                common.Concat([SettingChange.Set("EarlyTrendGateSide", 0m)]).ToList()));
        }
        return list;
    }

    /// <summary>
    /// The six independent opening-type groups: {GapUp, GapDown, Normal} x {CALL, PUT}. Each group restricts entries to
    /// sessions that opened that way, then sweeps the candidate thresholds inside the group, so every group is selected
    /// on its own evidence. The full sweep runs at <see cref="GridOptions.OpeningGapThresholdPct"/>; the other thresholds
    /// in <see cref="GridOptions.OpeningGapSensitivityPct"/> re-run the group baseline only, as a sensitivity check that
    /// the classification boundary is not itself driving the result.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildOpeningTypes(TradeSide side, GridOptions g)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var call = side == TradeSide.Call;
        var list = new List<BacktestConfiguration>();

        // (floor, ceiling): the live band first, then the proposed candidates for this side.
        (decimal Floor, decimal Ceiling)[] bands = call
            ? [(45m, 65m), (45m, 55m), (50m, 65m)]
            : [(28m, 38m), (28m, 35m), (35m, 42m)];

        var bodyKey = call ? "CallBodyPctMinimum" : "PutBodyPctMinimum";

        foreach (var opening in new[] { OpeningType.GapUp, OpeningType.GapDown, OpeningType.Normal })
        {
            EntryRules Rules(double thresholdPct) => new()
            {
                OpeningTypeFilter = opening,
                OpeningTypeThresholdPct = thresholdPct,
            };

            void Add(string name, string desc, double thresholdPct, params SettingChange[] changes) =>
                list.Add(new BacktestConfiguration(
                    $"{tag}|{opening}@{thresholdPct:0.00}|{name}", side, desc, changes.ToList(), Rules(thresholdPct)));

            var thr = g.OpeningGapThresholdPct;

            // The group's own baseline: live rules, restricted to this opening type. Everything else is measured against it.
            Add("Baseline", $"live rules, {opening} sessions only", thr);

            foreach (var (floor, ceiling) in bands)
            {
                var rsi = new[] { RsiFloorChange(side, floor), RsiCeilingChange(side, ceiling) };
                var rsiName = $"Rsi{floor:0}-{ceiling:0}";

                foreach (var gap in g.OpeningEmaGapPoints)
                foreach (var atr in g.OpeningAtrPercentMinimums)
                {
                    Add($"{rsiName}+Gap{gap:0.##}+Atr{atr:0.00}",
                        $"{opening} only, 5m RSI {floor:0}-{ceiling:0}, EMA gap {gap:0.##} pts, ATR% >= {atr:0.00}",
                        thr, [.. rsi, .. Gap(gap, g), .. Atr(atr)]);
                }

                // Candle-body confirmation, at the live gap and ATR so the body effect is readable on its own.
                Add($"{rsiName}+Body{g.OpeningBodyPctMinimum:0}",
                    $"{opening} only, 5m RSI {floor:0}-{ceiling:0}, candle body >= {g.OpeningBodyPctMinimum:0}%",
                    thr, [.. rsi, SettingChange.Set(bodyKey, g.OpeningBodyPctMinimum)]);
            }

            // Sensitivity: does the group survive a different gap boundary?
            foreach (var alt in g.OpeningGapSensitivityPct)
                Add("Baseline", $"live rules, {opening} sessions only (gap boundary {alt:0.00}%)", alt);
        }

        return list;
    }

    /// <summary>
    /// Sweeps the late-entry ("stretch") threshold: how many ATRs price may already have run beyond the
    /// 1m EMA20, in the trade's direction, before the entry is refused. The live rules have no such cap,
    /// so Baseline is the comparison. MinStretch arms are the contrast - if lateness were harmless, a
    /// configuration that takes ONLY late entries should not behave differently from the baseline.
    /// </summary>
    /// <summary>EMA9 above EMA21 (1m) on top of the live rules, per opening type; baseline first so every arm is measured against it.</summary>
    public static IReadOnlyList<BacktestConfiguration> BuildFastEma(TradeSide side, GridOptions g)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>();
        foreach (var opening in new[] { OpeningType.GapUp, OpeningType.GapDown, OpeningType.Normal })
        {
            var thr = g.OpeningGapThresholdPct;
            list.Add(new BacktestConfiguration($"{tag}|{opening}@{thr:0.00}|Baseline", side, $"live rules, {opening} only", [],
                new EntryRules { OpeningTypeFilter = opening, OpeningTypeThresholdPct = thr }));
            list.Add(new BacktestConfiguration($"{tag}|{opening}@{thr:0.00}|FastEma", side, $"{opening} only, live rules + 1m EMA9/EMA21 aligned", [],
                new EntryRules { OpeningTypeFilter = opening, OpeningTypeThresholdPct = thr, RequireFastEmaAligned = true }));
        }
        return list;
    }

    /// <summary>CALL only: the pasted gap-up momentum setup as a separate entry path, against the live rules on the same gap-up sessions.</summary>
    public static IReadOnlyList<BacktestConfiguration> BuildGapUpMomentum()
    {
        BacktestConfiguration Make(string name, string desc, EntryRules r) => new($"CALL|GUM|{name}", TradeSide.Call, desc, [], r);
        EntryRules Rules(decimal maxRsi, bool confirm) => new()
        {
            OpeningTypeFilter = OpeningType.GapUp,
            Trend = new TrendStrategyRules { Strategy = TrendStrategy.GapUpMomentum, MaxRsi5 = maxRsi, RequireAbovePrev5mHigh = confirm },
        };
        return
        [
            Make("Baseline", "live rules, gap-up sessions only", new EntryRules { OpeningTypeFilter = OpeningType.GapUp }),
            Make("Full", "gap>=0.30, 5m+1m EMA20>EMA50, price>EMA20, 5m RSI 45-85, ATR%>=0.03, 1m close > prev 5m high", Rules(85m, true)),
            Make("Rsi45-65", "Full with the live RSI ceiling 65", Rules(65m, true)),
            Make("NoConfirm", "Full without the previous-5m-high confirmation", Rules(85m, false)),
        ];
    }

    /// <summary>CALL on gap-up sessions: the early-trend gate (blocks CALL once 5m EMA20 is above EMA50) on vs off for BANKNIFTY only and for every symbol.</summary>
    public static IReadOnlyList<BacktestConfiguration> BuildGapUpCallGate()
    {
        var gapUp = new EntryRules { OpeningTypeFilter = OpeningType.GapUp };
        BacktestConfiguration Make(string name, string desc, params SettingChange[] changes) =>
            new($"CALL|GapUpGate|{name}", TradeSide.Call, desc, changes.ToList(), gapUp);
        return
        [
            Make("Live", "live rules, gap-up sessions only"),
            Make("GateOffBANKNIFTY", "early-trend gate off for BANKNIFTY only", SettingChange.Set("EnableEarlyTrendGate", 0m, "BANKNIFTY")),
            Make("GateOffAll", "early-trend gate off for every symbol", SettingChange.Set("EnableEarlyTrendGate", 0m)),
        ];
    }

    /// <summary>
    /// CALL, by opening type: the live EarlyTrendGate (A), the gate removed (B), and an exception that allows a CALL while the 5m EMA20-EMA50 gap
    /// is under X x ATR5m (C1-C5, X = 0.10..0.50, experimental values, not optimised).
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildEarlyTrendException(GridOptions g)
    {
        var list = new List<BacktestConfiguration>();
        foreach (var opening in new[] { OpeningType.GapUp, OpeningType.Normal, OpeningType.GapDown })
        {
            var thr = g.OpeningGapThresholdPct;
            void Add(string name, string desc, double? gapAtr, bool gateOff = false) =>
                list.Add(new BacktestConfiguration($"CALL|{opening}@{thr:0.00}|{name}", TradeSide.Call, desc,
                    gateOff ? [SettingChange.Set("EnableEarlyTrendGate", 0m)] : [],
                    new EntryRules { OpeningTypeFilter = opening, OpeningTypeThresholdPct = thr, EarlyTrendMaxGapAtr = gapAtr }));
            Add("A-Live", "live EarlyTrendGate", null);
            Add("B-GateOff", "EarlyTrendGate removed", null, gateOff: true);
            foreach (var x in new[] { 0.10, 0.20, 0.30, 0.40, 0.50 })
                Add($"C-{x:0.00}", $"allow CALL while 5m EMA20-EMA50 < {x:0.00} x ATR5m", x);
        }
        return list;
    }

    /// <summary>
    /// Gap-days-only, after-10:30 softer early-trend gate (HYPOTHESIS formed after inspecting 2026-11-11-early-trend-recheck; research only).
    /// A = live gate INCLUDING the live gap-up morning exception (gap >= 0.30%, before 10:00). E = A plus: a CALL is also allowed while
    /// (5m EMA20 - EMA50) / ATR5m < 0.30, but only on GapUp/GapDown sessions and only from 10:30 IST. Normal days keep the live gate.
    /// Run on all sessions and on GapUp / GapDown alone, so the same dates are compared. The gate is CALL-only (EarlyTrendGateSide 1), so PUT is unaffected.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildEarlyTrendGapDays(GridOptions g)
    {
        var list = new List<BacktestConfiguration>();
        var thr = g.OpeningGapThresholdPct;
        foreach (var opening in new[] { OpeningType.Any, OpeningType.GapUp, OpeningType.GapDown })
        {
            void Add(string name, string desc, bool softer) =>
                list.Add(new BacktestConfiguration($"CALL|{opening}@{thr:0.00}|{name}", TradeSide.Call, desc, [],
                    new EntryRules
                    {
                        OpeningTypeFilter = opening, OpeningTypeThresholdPct = thr,
                        GapMinPct = 0.30, EarlyTrendGapUpMorningUntilMinute = 10 * 60,
                        EarlyTrendMaxGapAtr = softer ? 0.30 : null,
                        EarlyTrendMaxGapAtrFromMinute = softer ? 10 * 60 + 30 : null,
                        EarlyTrendMaxGapAtrGapDaysOnly = softer,
                    }));
            Add("A-Live", "live gate + live gap-up morning exception", false);
            Add("E-GapDays1030", "A plus: allow CALL while EMA gap < 0.30 ATR5m, gap days only, from 10:30 IST", true);
        }
        return list;
    }

    /// <summary>
    /// Version D: the live EarlyTrendGate with one experimental exception - a CALL on a gap-up session (open >= 0.30% above the previous close,
    /// fixed before testing) is allowed before 10:00 / 10:15 / 10:30 IST. Run on all sessions (the complete strategy) and on gap-up and normal
    /// sessions alone; normal-day arms must equal A because the exception cannot fire there.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildGapUpMorningException(GridOptions g)
    {
        var list = new List<BacktestConfiguration>();
        foreach (var opening in new[] { OpeningType.Any, OpeningType.GapUp, OpeningType.Normal })
        {
            var thr = g.OpeningGapThresholdPct;
            void Add(string name, string desc, int? until) =>
                list.Add(new BacktestConfiguration($"CALL|{opening}@{thr:0.00}|{name}", TradeSide.Call, desc, [],
                    new EntryRules { OpeningTypeFilter = opening, OpeningTypeThresholdPct = thr, GapMinPct = thr, EarlyTrendGapUpMorningUntilMinute = until }));
            Add("A-Live", "live EarlyTrendGate", null);
            Add("D1-1000", "gap-up CALL exempt from the gate before 10:00", 10 * 60);
            Add("D2-1015", "gap-up CALL exempt from the gate before 10:15", 10 * 60 + 15);
            Add("D3-1030", "gap-up CALL exempt from the gate before 10:30", 10 * 60 + 30);
        }
        return list;
    }

    public static IReadOnlyList<BacktestConfiguration> BuildStretchSweep(TradeSide side, GridOptions g)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>();

        void Add(string name, string desc, EntryRules rules) =>
            list.Add(new BacktestConfiguration($"{tag}|Stretch|{name}", side, desc, [], rules));

        list.Add(new BacktestConfiguration($"{tag}|Stretch|Baseline", side,
            "live rules, no stretch cap", [], null));

        foreach (var max in g.StretchMaxAtr)
            Add($"Max{max:0.00}", $"refuse entries already more than {max:0.00} ATR beyond EMA20",
                new EntryRules { MaxStretchAtr = (double)max });

        foreach (var min in g.StretchMinAtr)
            Add($"Min{min:0.00}", $"contrast: take ONLY entries at least {min:0.00} ATR beyond EMA20",
                new EntryRules { MinStretchAtr = (double)min });

        return list;
    }

    /// <summary>
    /// Tests the position-slot rule itself. Production holds ONE position across all symbols, so a
    /// setup that appears while another trade is running is simply never taken. These arms raise the
    /// cap, make it per symbol, and separately remove the handoff cooldown - which serialises entries
    /// on its own and would otherwise mask any benefit from extra slots.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildSlotSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>();

        void Add(string name, string desc, SlotRules slots) =>
            list.Add(new BacktestConfiguration($"{tag}|Slots|{name}", side, desc, [], null, slots));

        Add("Baseline", "production: one position across all symbols", new SlotRules());
        Add("Global2", "two concurrent positions across all symbols", new SlotRules(2));
        Add("Global3", "three concurrent positions across all symbols", new SlotRules(3));
        Add("PerSymbol1", "one position per symbol (up to three at once)", new SlotRules(1, PerSymbol: true));
        Add("PerSymbol2", "two positions per symbol", new SlotRules(2, PerSymbol: true));
        Add("NoHandoff", "production slots, handoff cooldown off", new SlotRules(1, NoHandoffCooldown: true));
        Add("Global2+NoHandoff", "two slots and no handoff cooldown", new SlotRules(2, NoHandoffCooldown: true));
        Add("PerSymbol1+NoHandoff", "one per symbol and no handoff cooldown", new SlotRules(1, true, true));
        Add("Unlimited", "no slot cap and no handoff cooldown (upper bound, not a proposal)",
            new SlotRules(99, true, true));
        return list;
    }

    /// <summary>
    /// Does the stretch cap help or hurt once the session is split by how it opened? On a gap-down
    /// open price starts far below EMA20, so a PUT cap may refuse exactly the continuation entries
    /// that should work. Each opening type is run with and without the cap so the two are comparable
    /// on the same sessions.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildStretchByOpening(TradeSide side, GridOptions g)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>();
        foreach (var opening in new[] { OpeningType.GapUp, OpeningType.GapDown, OpeningType.Normal })
        {
            list.Add(new BacktestConfiguration($"{tag}|{opening}|NoCap", side,
                $"{opening} sessions, live rules", [],
                new EntryRules { OpeningTypeFilter = opening }));
            foreach (var max in g.StretchMaxAtr)
                list.Add(new BacktestConfiguration($"{tag}|{opening}|Max{max:0.00}", side,
                    $"{opening} sessions, cap {max:0.00} ATR", [],
                    new EntryRules { OpeningTypeFilter = opening, MaxStretchAtr = (double)max }));
        }
        return list;
    }

    /// <summary>
    /// The conditional stretch cap: apply it only when the session so far is round-tripping
    /// (low efficiency). Baseline and the flat cap are included so the three are comparable on the
    /// same sessions, and both "unknown" behaviours are run because the early-session case cannot be
    /// reasoned out in advance.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildEfficiencySweep(TradeSide side, GridOptions g)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>
        {
            new($"{tag}|Eff|Baseline", side, "live rules, no cap", [], null),
        };

        var cap = g.StretchMaxAtr.Length > 0 ? g.StretchMaxAtr[0] : 0.85m;
        list.Add(new BacktestConfiguration($"{tag}|Eff|FlatCap{cap:0.00}", side,
            $"cap {cap:0.00} ATR on every session", [],
            new EntryRules { MaxStretchAtr = (double)cap }));

        foreach (var eff in g.EfficiencyThresholds)
        foreach (var capUnknown in new[] { true, false })
            list.Add(new BacktestConfiguration(
                $"{tag}|Eff|Below{eff:0.00}{(capUnknown ? "+CapEarly" : "+FreeEarly")}", side,
                $"cap {cap:0.00} ATR only when session efficiency < {eff:0.00}; " +
                $"{(capUnknown ? "cap" : "allow")} while the range is too small to judge", [],
                new EntryRules
                {
                    MaxStretchAtr = (double)cap,
                    CapOnlyWhenEfficiencyBelow = (double)eff,
                    CapWhenEfficiencyUnknown = capUnknown,
                }));
        return list;
    }

    /// <summary>
    /// THE BASE RATE. Every entry filter off (RSI, PCR, ATR, EMA gap, early-trend, no-trade window)
    /// and every queueing limit off (unlimited concurrent positions, no handoff cooldown), so each
    /// decision minute that has a tradable option becomes a measured trade. This is the unbiased
    /// sample: what the market did from EVERY minute, not just the ones the live band admits.
    /// Bucket the dumped trades by their EntryContext to see which RSI / PCR values actually paid.
    /// Not a strategy - a measuring instrument.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildBaseRate(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        SettingChange[] allOff =
        [
            SettingChange.Set("EnableRsiFilter", 0m),
            .. (side == TradeSide.Call ? new[] { SettingChange.Off("CallRsiFloor"), SettingChange.Off("CallRsiCeiling") } : []),
            SettingChange.Set(side == TradeSide.Call ? "EnableCallPcrFilter" : "EnablePutPcrFilter", 0m),
            SettingChange.Set("EnableAtrFilter", 0m),
            SettingChange.Set("EnableEmaGapFilter", 0m),
            SettingChange.Set("EnableEarlyTrendGate", 0m),
            SettingChange.Set("EnableNoTradeWindowGate", 0m),
        ];
        var unlimited = new SlotRules(99, PerSymbol: true, NoHandoffCooldown: true);
        return
        [
            new($"{tag}|Base|AllFiltersOff", side, "every entry filter and queue limit off", allOff, null, unlimited),
            new($"{tag}|Base|LiveForReference", side, "live rules, for comparison", [], null, null),
        ];
    }

    /// <summary>
    /// A gap-up-specific PUT RSI band. On the unfiltered base rate the live 28-38 band is NEGATIVE on
    /// gap-up sessions (-254/trade) while 45-60 pays (+193/trade, n=128): a PUT on a gap-up day works
    /// when the market is rolling over from strength, not when it is already oversold. The candidate
    /// bands here were chosen from that unfiltered measurement, then tested walk-forward.
    /// Blocking the side on gap-up sessions is included as the simpler alternative.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildOpeningBandSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>
        {
            new($"{tag}|OpenBand|Baseline", side, "live rules, one band on every session", [], null),
            new($"{tag}|OpenBand|BlockOnGapUp", side, "refuse this side on gap-up sessions", [],
                new EntryRules { OpeningBandAppliesTo = OpeningType.GapUp, OpeningBandBlocks = true }),
            new($"{tag}|OpenBand|BlockOnGapDown", side, "refuse this side on gap-down sessions", [],
                new EntryRules { OpeningBandAppliesTo = OpeningType.GapDown, OpeningBandBlocks = true }),
        };
        // Side-appropriate candidate bands, each picked from the unfiltered snapshot forward-move
        // measurement (docs/research/2026-10-16-snapshot-forward/) rather than from a gated sweep.
        var bands = side == TradeSide.Call
            ? new[] { (70m, 100m), (65m, 100m), (75m, 100m) }
            : new[] { (45m, 60m), (45m, 100m), (50m, 60m), (50m, 100m), (30m, 35m) };
        foreach (var (lo, hi) in bands)
            list.Add(new BacktestConfiguration($"{tag}|OpenBand|GapUp{lo:0}-{hi:0}", side,
                $"on gap-up sessions use RSI {lo:0}-{hi:0}; the live band elsewhere", [],
                new EntryRules
                {
                    OpeningBandAppliesTo = OpeningType.GapUp,
                    OpeningBandRsiFloor = lo,
                    OpeningBandRsiCeiling = hi,
                }));
        return list;
    }

    /// <summary>
    /// Stop x target width grid on the unmodified live entry rules. The live arm (stop 1.25, T1 1.5, T2 1.95 ATR) is the baseline;
    /// T2 keeps the live 1.3 x T1 ratio. The ladder levels stay whatever the live StrategySettings say.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildExitGeometrySweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>();
        foreach (var stop in new[] { 1.0, 1.1, 1.25 })
            foreach (var t1 in new[] { 1.5, 2.0, 3.0 })
            {
                var live = stop == 1.25 && t1 == 1.5;
                list.Add(new BacktestConfiguration($"{tag}|Exit|S{stop:0.00}-T{t1:0.0}", side,
                    live ? "live exits: stop 1.25, T1 1.5, T2 1.95 ATR (baseline)" : $"stop {stop:0.00} ATR, T1 {t1:0.0} ATR, T2 {t1 * 1.3:0.00} ATR",
                    [], null, null, null,
                    live ? null : new ExitGeometry(stop, t1, Math.Round(t1 * 1.3, 2))));
            }
        return list;
    }

    /// <summary>
    /// OiConfirmationGate (ATM +/- 2 OI change in step with the last 1m price change), replayed only where option history exists
    /// (2026-10-07 onward). Live: ON for BANKNIFTY, OFF for NIFTY and SENSEX. Arms: live, BANKNIFTY off, on everywhere, off everywhere.
    /// The gate holds a built setup until a later minute confirms, so a rejection delays the entry rather than only removing it.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildOiGateSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        BacktestConfiguration Arm(string name, string desc, params SettingChange[] changes) =>
            new($"{tag}|OiGate|{name}", side, desc, changes.ToList());
        return
        [
            Arm("Live", "live: BANKNIFTY on, NIFTY and SENSEX off",
                SettingChange.Set("EnableOiConfirmationGate", 1m, "BANKNIFTY"),
                SettingChange.Set("EnableOiConfirmationGate", 0m, "NIFTY"),
                SettingChange.Set("EnableOiConfirmationGate", 0m, "SENSEX")),
            Arm("OffBANKNIFTY", "gate off everywhere",
                SettingChange.Set("EnableOiConfirmationGate", 0m, "BANKNIFTY"),
                SettingChange.Set("EnableOiConfirmationGate", 0m, "NIFTY"),
                SettingChange.Set("EnableOiConfirmationGate", 0m, "SENSEX")),
            Arm("OnAll", "gate on for every symbol",
                SettingChange.Set("EnableOiConfirmationGate", 1m, "BANKNIFTY"),
                SettingChange.Set("EnableOiConfirmationGate", 1m, "NIFTY"),
                SettingChange.Set("EnableOiConfirmationGate", 1m, "SENSEX")),
        ];
    }

    /// <summary>
    /// Index stop floor (IndexStopFloorGate: blocks an entry when AtrRaw is below MinimumIndexStopPoints / floor multiplier, 13.33 pts
    /// at 20 / 1.5). Off lets the entry through with the stop clamped up to the minimum (20 pts), i.e. wider risk than 1.25 ATR.
    /// Arms: live, floor off for one symbol at a time, floor off everywhere. Live entries otherwise.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildStopFloorSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration>
        {
            new($"{tag}|Floor|Live", side, "live index stop floor", []),
            new($"{tag}|Floor|OffAll", side, "floor off for every symbol (stop clamps to the minimum)", [SettingChange.Set("EnableIndexStopFloor", 0m)]),
        };
        foreach (var sym in ExitModel.SymbolNames)
            list.Add(new($"{tag}|Floor|Off{sym}", side, $"floor off for {sym} only", [SettingChange.Set("EnableIndexStopFloor", 0m, sym)]));
        return list;
    }

    /// <summary>
    /// Exit comparison on the unmodified live entries: Legacy (T1 half, T2 full exit), Ladder (T1 half, 3R half, 5R rest) and
    /// T2Partial (T1 half, T2 half of the rest, last lot trails). Every symbol is set explicitly so the arms never inherit the
    /// live ladder/T2 flags. Needs 3+ lots (AdaptiveLotMin) - with fewer, T2Partial is identical to Legacy.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildT2PartialComparison(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        BacktestConfiguration Arm(string name, string desc, bool ladder, bool t2) =>
            new($"{tag}|Exit|{name}", side, desc,
            [
                SettingChange.Set("EnableTargetLadder", ladder ? 1m : 0m),
                SettingChange.Set("EnableT2Partial", t2 ? 1m : 0m),
            ]);
        return
        [
            Arm("Legacy", "T1 half, T2 full exit (baseline)", false, false),
            Arm("Ladder", "T1 half, 3R half of the rest, 5R the last", true, false),
            Arm("T2Partial", "T1 half, T2 half of the rest, last lot trails", false, true),
        ];
    }

    /// <summary>
    /// Two PUT candidates taken from the IndicatorSnapshots analysis (1-minute RSI, ATR% window, EMA alignment), each in three arms:
    /// Replace = the candidate is the entry rule (safety gates kept), ReplaceNoFloor = the same with the index-stop floor off to show what
    /// that gate removes, AndChain = the live chain must pass as well. Baseline = live rules restricted to the same opening type.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildProposedCandidates(TradeSide side)
    {
        if (side != TradeSide.Put) return [];
        var noFloor = new[] { SettingChange.Set("EnableIndexStopFloor", 0m) };
        var list = new List<BacktestConfiguration>();
        foreach (var (name, opening, atrMin, atrMax, alignment) in new[]
        {
            ("C1", OpeningType.GapDown, 0.0, 0.03, EmaAlignment.Bearish),
            ("C3", OpeningType.GapUp, 0.04, 0.05, EmaAlignment.Bearish),
        })
        {
            ProposedEntryRule Rule(bool replace) => new()
            {
                Opening = opening, Rsi1mFloor = 45, Rsi1mCeiling = 55, AtrPctMin = atrMin, AtrPctMax = atrMax,
                Alignment = alignment, ReplacesChain = replace,
            };
            var label = $"{opening} PUT, 1m RSI 45-55, ATR% {atrMin}-{atrMax}, {alignment} alignment";
            list.Add(new BacktestConfiguration($"PUT|{name}|Baseline", side, $"live rules on {opening} sessions only", [],
                new EntryRules { OpeningTypeFilter = opening }));
            list.Add(new BacktestConfiguration($"PUT|{name}|Replace", side, $"{label}; replaces the live chain", [],
                new EntryRules { Proposed = Rule(true) }));
            list.Add(new BacktestConfiguration($"PUT|{name}|ReplaceNoFloor", side, $"{label}; replaces the live chain, index-stop floor off", noFloor,
                new EntryRules { Proposed = Rule(true) }));
            list.Add(new BacktestConfiguration($"PUT|{name}|AndChain", side, $"{label}; live chain must also pass", [],
                new EntryRules { Proposed = Rule(false) }));
        }
        return list;
    }

    /// <summary>
    /// PUT on gap-down sessions. "Continuation" is only knowable at the close, so these use the
    /// entry-time observables that the snapshot forward-move measurement says track it: the session
    /// opened down (known at the open) and the time of day (the gap-down drift is concentrated
    /// 11:00-14:00 IST, peaking at 12:00 with -3.57 ATR / 75% down over the next 30 minutes).
    /// The live no-trade window sits at 12:00-12:30, so an arm with it off is included.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildGapDownPutSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var noWindow = new[] { SettingChange.Set("EnableNoTradeWindowGate", 0m) };
        List<BacktestConfiguration> list =
        [
            new($"{tag}|GD|Baseline", side, "live rules", [], null),
            new($"{tag}|GD|NoTradeWindowOff", side, "live rules, 12:00-12:30 window off", noWindow, null),
            new($"{tag}|GD|GapDownOnly", side, "only sessions that opened gap-down", [],
                new EntryRules { OpeningTypeFilter = OpeningType.GapDown }),
            new($"{tag}|GD|GapDownOnly+NoWindow", side, "gap-down sessions, no-trade window off", noWindow,
                new EntryRules { OpeningTypeFilter = OpeningType.GapDown }),
        ];
        foreach (var (from, to, name) in new[] { (11, 14, "11-14"), (11, 15, "11-15"), (12, 14, "12-14") })
        {
            list.Add(new BacktestConfiguration($"{tag}|GD|GapDown+{name}", side,
                $"gap-down sessions, entries {from:00}:00-{to:00}:00 IST only", noWindow,
                new EntryRules
                {
                    OpeningTypeFilter = OpeningType.GapDown,
                    EntryFromMinuteIst = from * 60, EntryToMinuteIst = to * 60,
                }));
            list.Add(new BacktestConfiguration($"{tag}|GD|AllDays+{name}", side,
                $"every session, entries {from:00}:00-{to:00}:00 IST only", noWindow,
                new EntryRules { EntryFromMinuteIst = from * 60, EntryToMinuteIst = to * 60 }));
        }
        return list;
    }

    /// <summary>
    /// CONTROL EXPERIMENT: is a filter's benefit information, or just fewer trades?
    /// At ~Rs350/trade a chain running 146 trades pays ~51,000 in costs, so ANY reduction helps.
    /// These arms cut the trade count WITHOUT using market information - every Nth entry, a cap per
    /// symbol-day, and a time window the snapshot drift says is dead (10:00-11:00). If an informed
    /// filter does no better than these at the same trade count, it is not adding information.
    /// </summary>
    /// <summary>
    /// Experiment: switch the live previous-5-minute level condition ON (CALL: LTP above the last closed 5m high,
    /// PUT: below its low) against the live rules, where it is off. LTP is already a closed 1m close.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildBreakoutLevel(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var key = side == TradeSide.Call ? "EnableCallPrev5mHighFilter" : "EnablePutPrev5mLowFilter";
        return
        [
            new($"{tag}|Breakout|Baseline", side, "live rules (level filter off)", [], null),
            new($"{tag}|Breakout|Prev5mLevelOn", side,
                side == TradeSide.Call ? "CALL also needs a close above the last closed 5m high"
                                       : "PUT also needs a close below the last closed 5m low",
                [SettingChange.Set(key, 1m)]),
        ];
    }

    /// <summary>
    /// Diagnostic: the live rules with CALL and PUT sharing ONE position slot and shared cooldowns (production's OnePositionGate),
    /// the closest the replay gets to live occupancy. Compare with the per-side runs, which let a CALL and a PUT overlap.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildSharedSlot() =>
    [
        new("BOTH|SharedSlot", TradeSide.Call, "live rules, CALL and PUT share one position slot", [], null, new SlotRules(SharedBothSides: true)),
    ];

    public static IReadOnlyList<BacktestConfiguration> BuildThinningControl(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        List<BacktestConfiguration> list =
        [
            new($"{tag}|Thin|Baseline", side, "live rules", [], null),
            new($"{tag}|Thin|GapDownOnly", side, "informed: gap-down sessions only", [],
                new EntryRules { OpeningTypeFilter = OpeningType.GapDown }),
            new($"{tag}|Thin|DeadWindow10-11", side,
                "control: 10:00-11:00 only, a window the drift says is dead", [],
                new EntryRules { EntryFromMinuteIst = 10 * 60, EntryToMinuteIst = 11 * 60 }),
        ];
        foreach (var n in new[] { 2, 3, 4 })
            list.Add(new BacktestConfiguration($"{tag}|Thin|KeepEvery{n}", side,
                $"control: every {n}th valid entry, no information", [], null, null,
                new SamplingRules(KeepEveryNth: n)));
        foreach (var cap in new[] { 1, 2 })
            list.Add(new BacktestConfiguration($"{tag}|Thin|MaxPerDay{cap}", side,
                $"control: at most {cap} trade(s) per symbol-day", [], null, null,
                new SamplingRules(MaxTradesPerSymbolDay: cap)));
        return list;
    }

    /// <summary>
    /// "Gap up and going up and up" / "gap down and going down and down" / the same on a normal open.
    /// One rule parameterised by opening and direction: the session must have travelled MinMoveAtr in
    /// the trade's direction with efficiency at least MinEfficiency (not round-tripped).
    ///
    /// THRESHOLDS ARE FIXED IN ADVANCE (0.75 ATR / 0.50 efficiency) and NOT tuned on the result -
    /// this dataset's noise is about +/-35,000 (see the KeepEvery* controls), so a tuned threshold
    /// would just be fitting that. The control arms are included in the same run for comparison.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildSessionTrendSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        const double move = 0.75, eff = 0.50;

        List<BacktestConfiguration> list =
        [
            new($"{tag}|ST|Baseline", side, "live rules", [], null),
            // Controls: information-free thinning, so a result can be compared with arbitrary cuts.
            new($"{tag}|ST|KeepEvery2", side, "control: every 2nd valid entry", [], null, null, new SamplingRules(2)),
            new($"{tag}|ST|KeepEvery4", side, "control: every 4th valid entry", [], null, null, new SamplingRules(4)),
        ];

        EntryRules Rule(OpeningType scope, double? nearExtreme = null) => new()
        {
            SessionTrendMinMoveAtr = move,
            SessionTrendMinEfficiency = eff,
            SessionTrendAppliesTo = scope,
            SessionTrendNearExtremeAtr = nearExtreme,
        };

        foreach (var scope in new[] { OpeningType.Any, OpeningType.GapUp, OpeningType.GapDown, OpeningType.Normal })
        {
            list.Add(new BacktestConfiguration($"{tag}|ST|{scope}", side,
                $"session must have run {move:0.00} ATR with efficiency >= {eff:0.00} ({scope} openings)",
                [], Rule(scope)));
            list.Add(new BacktestConfiguration($"{tag}|ST|{scope}+AtExtreme", side,
                $"as above, and within 1.00 ATR of the session extreme ({scope} openings)",
                [], Rule(scope, nearExtreme: 1.0)));
        }
        return list;
    }

    private static decimal[] RsiFloors(TradeSide s, GridOptions g) => s == TradeSide.Call ? g.CallRsiFloors : g.PutRsiFloors;
    private static decimal[] RsiCeilings(TradeSide s, GridOptions g) => s == TradeSide.Call ? g.CallRsiCeilings : g.PutRsiCeilings;
    private static decimal[] PcrMinimums(TradeSide s, GridOptions g) => s == TradeSide.Call ? g.CallPcrMinimums : g.PutPcrMinimums;

    private static SettingChange RsiFloorChange(TradeSide s, decimal v) => SettingChange.Set(s == TradeSide.Call ? "CallRsiFloor" : "PutRsiFloor", v);
    private static SettingChange RsiCeilingChange(TradeSide s, decimal v) => SettingChange.Set(s == TradeSide.Call ? "CallRsiCeiling" : "PutRsiCeiling", v);

    /// <summary>
    /// Entry-timing study: the live AND-chain with exactly ONE condition removed at a time, to find which condition holds a
    /// CALL back until the move is already old. Delta (condition 10) is not replayable (the backtest has no option delta), so
    /// it is not listed. EMA20&gt;EMA50 and LTP&gt;EMA20 are the trend itself and are never removed.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildChainLeaveOneOut(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        BacktestConfiguration Make(string name, string desc, params SettingChange[] changes) =>
            new($"{tag}|LOO|{name}", side, desc, changes);

        var list = new List<BacktestConfiguration> { Make("Baseline", "live rules, nothing removed") };
        list.AddRange(
        [
            Make("NoRsi", "RSI band removed", NoRsi(side)),
            Make("NoEmaGap", "EMA-gap filter removed", NoGap()),
            Make("NoAtr", "ATR% gate removed", NoAtr()),
            Make("NoPcr", "PCR filter removed", NoPcr(side)),
            Make("NoCandle", "bullish-candle + body% condition removed", SettingChange.Set("EnableCandleFilter", 0m)),
        ]);
        if (side == TradeSide.Call)
        {
            list.Add(Make("NoPrev5mHigh", "LTP > previous 5m high removed", SettingChange.Set("EnableCallPrev5mHighFilter", 0m)));
            list.Add(Make("NoDayChange", "day-change / recovery condition removed", SettingChange.Off("CallDayChangePctMin"), SettingChange.Off("CallRecoveryPctMin")));
            list.Add(Make("NoEarlyTrendGate", "early-trend gate off", SettingChange.Set("EnableEarlyTrendGate", 0m)));
        }
        return list;
    }

    /// <summary>
    /// Rated direction (no RSI): live baseline plus the CALL-vs-PUT score strategy over a MinScore x MinMargin grid. Each side is
    /// run as its own configuration, so a side enters only on the minutes it won the score.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildRatedDirectionSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var list = new List<BacktestConfiguration> { new($"{tag}|RATED|Live", side, "live AND-chain (baseline)", []) };
        foreach (var score in new[] { 50m, 55m, 60m, 65m, 70m, 75m, 80m, 85m, 90m })
            foreach (var margin in new[] { 0m, 10m, 20m, 30m })
                list.Add(new BacktestConfiguration($"{tag}|RATED|S{score:0}-M{margin:0}", side,
                    $"rated direction, score >= {score:0}, margin >= {margin:0}", [],
                    new EntryRules { Rated = new RatedDirectionRules(score, margin) }));
        return list;
    }

    /// <summary>
    /// Live-condition interchange: every ON/OFF combination of the switchable live entry conditions (2^k arms, live
    /// thresholds kept), so each condition's contribution is seen with and without the others. The trend itself
    /// (EMA20 vs EMA50, LTP vs EMA20) is never removed; EarlyTrendGate is CALL-only (EarlyTrendGateSide = 1).
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildLiveInterchange(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        var toggles = new List<(string Name, SettingChange[] Off)>
        {
            ("Rsi", NoRsi(side)),
            ("EmaGap", NoGap()),
            ("Atr", NoAtr()),
            ("Pcr", NoPcr(side)),
        };
        if (side == TradeSide.Call)
            toggles.Add(("EarlyTrendGate", [SettingChange.Set("EnableEarlyTrendGate", 0m)]));

        var list = new List<BacktestConfiguration>();
        for (var mask = 0; mask < 1 << toggles.Count; mask++)
        {
            var off = toggles.Where((_, i) => (mask & (1 << i)) != 0).ToList();
            var name = off.Count == 0 ? "Live" : "Off-" + string.Join("+", off.Select(t => t.Name));
            var desc = off.Count == 0 ? "live rules, all conditions on" : "live rules with " + string.Join(", ", off.Select(t => t.Name)) + " off";
            list.Add(new BacktestConfiguration($"{tag}|MIX|{name}", side, desc, off.SelectMany(t => t.Off).ToArray()));
        }
        return list;
    }

    /// <summary>
    /// Entry-timing study: which MarketState holds early CALLs back, and what allowing Directional in it would have earned.
    /// Every arm labels the blocking state; arms only differ in which states may run.
    /// </summary>
    public static IReadOnlyList<BacktestConfiguration> BuildSelectorStateSweep(TradeSide side)
    {
        var tag = side == TradeSide.Call ? "CALL" : "PUT";
        BacktestConfiguration Make(string name, string desc, params string[] allow) =>
            new($"{tag}|SEL|{name}", side, desc, [],
                new EntryRules { LabelSelectorState = true, SelectorAllowStates = allow.Length == 0 ? null : allow });
        return
        [
            Make("Baseline", "live routing, blocking state labelled"),
            Make("AllowReversal", "Directional also runs in Reversal", "Reversal"),
            Make("AllowRange", "Directional also runs in Range", "Range"),
            Make("AllowReversal+Range", "Directional also runs in Reversal and Range", "Reversal", "Range"),
        ];
    }

    private static SettingChange[] NoRsi(TradeSide s) => s == TradeSide.Call
        ? [SettingChange.Off("CallRsiFloor"), SettingChange.Off("CallRsiCeiling")]
        : [];   // PUT has no RSI band

    private static SettingChange[] NoGap() => [SettingChange.Set("EnableEmaGapFilter", 0m)];
    private static SettingChange[] NoAtr() => [SettingChange.Set("EnableAtrFilter", 0m)];

    private static SettingChange[] NoPcr(TradeSide s) =>
        [SettingChange.Set(s == TradeSide.Call ? "EnableCallPcrFilter" : "EnablePutPcrFilter", 0m)];

    private static SettingChange[] Gap(decimal gap, GridOptions g) =>
    [
        SettingChange.Set("EmaGapMinPoints", gap),
        SettingChange.Set("EmaGapMinPoints", Math.Round(gap * g.BankNiftyGapRatio, 2), "BANKNIFTY"),
    ];

    private static SettingChange[] Pcr(TradeSide s, decimal v) => s == TradeSide.Call
        ? [SettingChange.Set("CallPcrMinimum", v)]
        : [SettingChange.Set("PutPcrMinimum", v, "SENSEX")];

    private static SettingChange[] Atr(decimal v) => Symbols.Select(sym => SettingChange.Set("MinAtrPercent", v, sym)).ToArray();
}
