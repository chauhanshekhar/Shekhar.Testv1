using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Only enter when the session SO FAR is travelling in the trade's direction: "gap up and going up
/// and up", "gap down and going down and down", and the same on a normal open.
///
/// Three measures, all from values known at this minute (nothing reads the session close):
///
///   trendSoFar = (Ltp - DayOpen) / AtrRaw            signed, in ATRs; a CALL wants +, a PUT -
///   efficiency = |Ltp - DayOpen| / (DayHigh - DayLow)   0 = round-tripped, 1 = one-way
///   offExtreme = distance from the session high (CALL) or low (PUT), in ATRs   [optional]
///
/// The opening is (DayOpen - YesterdayClose) / YesterdayClose, so the gate can be scoped to gap-up,
/// gap-down or normal sessions, or left to apply to all of them.
///
///   EnableSessionTrendGate      0/1                                        (default OFF)
///   SessionTrendMinMoveAtr      ATRs the session must have travelled        (default 0.75)
///   SessionTrendMinEfficiency   0..1, how one-way that travel must be       (default 0.50)
///   SessionTrendNearExtremeAtr  <= 0 disables the "still at the edge" test  (default 0, off)
///   SessionTrendOpening         0 = any, 1 = gap-up, 2 = gap-down, 3 = normal   (default 0)
///   SessionTrendGapPct          gap % separating the openings               (default 0.30)
///   SessionTrendSide            0 = CALL and PUT, 1 = CALL only, 2 = PUT only   (default 0)
///
/// MEASURED RESULT, read before enabling this. On the stored history (68/55/67 sessions, costed at
/// Rs20/order + 1pt slippage) every variant of this rule LOST money against the live baseline for
/// PUT, and an information-free control that keeps every 4th trade beat all of them
/// (docs/research/2026-10-16-session-trend/). The reading is that this system's PUT edge is in
/// entering BEFORE extension, not after confirmation - consistent with the EMA20-distance and
/// stretch-cap findings. It is OFF by default and should stay off for PUT unless a later run on
/// more data says otherwise. CALL was untestable (5 training trades).
///
/// If AtrRaw or DayOpen is unavailable (0) the gate lets the signal through, like the other entry
/// gates. HeroZero is exempt.
/// </summary>
public class SessionTrendGate : IPostSignalGate
{
    internal const string EnableKey = "EnableSessionTrendGate";
    internal const string MinMoveKey = "SessionTrendMinMoveAtr";
    internal const string MinEfficiencyKey = "SessionTrendMinEfficiency";
    internal const string NearExtremeKey = "SessionTrendNearExtremeAtr";
    internal const string OpeningKey = "SessionTrendOpening";
    internal const string GapPctKey = "SessionTrendGapPct";
    internal const string SideKey = "SessionTrendSide";

    internal const int OpeningAny = 0, OpeningGapUp = 1, OpeningGapDown = 2, OpeningNormal = 3;
    internal const int SideBoth = 0, SideCallOnly = 1, SidePutOnly = 2;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var reason = Evaluate(
            signal.SignalType,
            market.Ltp, market.DayOpen, market.DayHigh, market.DayLow, market.AtrRaw, market.YesterdayClose,
            minMoveAtr: ctx.StrategySettings.GetDecimal(MinMoveKey, ctx.Symbol, 0.75m),
            minEfficiency: ctx.StrategySettings.GetDecimal(MinEfficiencyKey, ctx.Symbol, 0.50m),
            nearExtremeAtr: ctx.StrategySettings.GetDecimal(NearExtremeKey, ctx.Symbol, 0m),
            opening: ctx.StrategySettings.GetInt(OpeningKey, ctx.Symbol, OpeningAny),
            gapPct: ctx.StrategySettings.GetDecimal(GapPctKey, ctx.Symbol, 0.30m),
            side: ctx.StrategySettings.GetInt(SideKey, ctx.Symbol, SideBoth));

        return Task.FromResult(reason is null ? null : $"{reason} ({ctx.Symbol})");
    }

    /// <summary>Whether the gate applies to this direction under the configured side.</summary>
    internal static bool AppliesTo(int side, string signalType) => side switch
    {
        SideCallOnly => signalType == TradingConst.SignalType.CALL,
        SidePutOnly => signalType == TradingConst.SignalType.PUT,
        _ => true,
    };

    /// <summary>Which opening the session had, or null when the previous close is unknown.</summary>
    internal static int? ClassifyOpening(decimal dayOpen, decimal yesterdayClose, decimal gapPct)
    {
        if (dayOpen <= 0 || yesterdayClose <= 0) return null;
        var gap = (dayOpen - yesterdayClose) / yesterdayClose * 100m;
        return gap >= gapPct ? OpeningGapUp
             : gap <= -gapPct ? OpeningGapDown
             : OpeningNormal;
    }

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(
        string signalType, decimal ltp, decimal dayOpen, decimal dayHigh, decimal dayLow,
        decimal atrRaw, decimal yesterdayClose,
        decimal minMoveAtr, decimal minEfficiency, decimal nearExtremeAtr,
        int opening, decimal gapPct, int side)
    {
        if (!AppliesTo(side, signalType))
            return null;

        // Fail open on missing inputs, like the other entry gates.
        if (atrRaw <= 0 || dayOpen <= 0 || ltp <= 0)
            return null;

        if (opening != OpeningAny)
        {
            var actual = ClassifyOpening(dayOpen, yesterdayClose, gapPct);
            if (actual is null || actual != opening)
                return null;   // out of scope for this session
        }

        var isCall = signalType == TradingConst.SignalType.CALL;
        var sign = isCall ? 1m : -1m;
        var direction = isCall ? "up" : "down";

        var trendSoFar = sign * (ltp - dayOpen) / atrRaw;
        if (trendSoFar < minMoveAtr)
            return $"Session-trend gate: session has moved {trendSoFar:F2} ATR {direction} from the open " +
                   $"({dayOpen:F2} -> {ltp:F2}), needs {minMoveAtr:F2} - {signalType} only enters a session already going {direction}";

        var range = dayHigh - dayLow;
        if (range <= 0)
            return null;   // no range yet; nothing to judge

        var efficiency = Math.Abs(ltp - dayOpen) / range;
        if (efficiency < minEfficiency)
            return $"Session-trend gate: session efficiency {efficiency:F2} < {minEfficiency:F2} " +
                   $"(range {range:F2}, net move {Math.Abs(ltp - dayOpen):F2}) - the session is round-tripping, not going {direction}";

        if (nearExtremeAtr > 0)
        {
            var offExtreme = isCall ? (dayHigh - ltp) / atrRaw : (ltp - dayLow) / atrRaw;
            if (offExtreme > nearExtremeAtr)
                return $"Session-trend gate: {offExtreme:F2} ATR off the session {(isCall ? "high" : "low")}, " +
                       $"needs <= {nearExtremeAtr:F2} - the move has stalled";
        }

        return null;
    }
}
