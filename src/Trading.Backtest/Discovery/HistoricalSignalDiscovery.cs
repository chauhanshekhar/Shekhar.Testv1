using Trading.Backtest.Data;

namespace Trading.Backtest.Discovery;

/// <summary>
/// Generates fresh CALL/PUT candidates from raw IndicatorSnapshots and evaluates only candles occurring after entry.
/// No old signal/selector/gate columns are referenced anywhere in this class.
/// </summary>
public sealed class HistoricalSignalDiscovery
{
    private readonly DiscoveryOptions _options;
    public HistoricalSignalDiscovery(DiscoveryOptions? options = null) => _options = options ?? new DiscoveryOptions();

    public IReadOnlyList<DiscoveryTrade> Run(
        IReadOnlyList<DiscoverySnapshotRow> snapshots,
        IReadOnlyDictionary<string, Bar[]> barsBySymbol,
        IReadOnlyList<DiscoveryRule> rules)
    {
        var output = new List<DiscoveryTrade>();
        foreach (var snap in snapshots)
        {
            if (!barsBySymbol.TryGetValue(snap.Symbol, out var bars) || bars.Length < 2) continue;
            if (snap.Ltp <= 0 || snap.AtrRaw <= 0 || snap.Ema20 <= 0 || snap.Ema50 <= 0) continue;

            var snapshotUtc = DateTime.SpecifyKind(snap.SnapshotTime - SessionClock.IstOffset, DateTimeKind.Utc);
            var entryIndex = UpperBound(bars, snapshotUtc);
            if (entryIndex >= bars.Length || bars[entryIndex].OpenUtc.Date != snapshotUtc.Date) continue;
            var entryBar = bars[entryIndex];
            if (entryBar.Open <= 0) continue;

            var gapPct = snap.YesterdayClose > 0
                ? (snap.DayOpen - snap.YesterdayClose) / snap.YesterdayClose * 100m : 0m;
            var gapGroup = gapPct >= 0.30m ? "GAP-UP" : gapPct <= -0.30m ? "GAP-DOWN" : "NORMAL";
            var mins = snap.SnapshotTime.Hour * 60 + snap.SnapshotTime.Minute;
            var session = mins < 11 * 60 ? "OPEN-TO-11" : mins < 13 * 60 ? "11-TO-13" : "AFTER-13";

            foreach (var rule in rules)
            {
                if (!Matches(snap, rule)) continue;
                var isCall = rule.Side == DiscoverySide.Call;
                var entry = (decimal)entryBar.Open;
                var atr = snap.AtrRaw;
                var target = entry + (isCall ? 1 : -1) * atr * _options.TargetAtr;
                var stop = entry - (isCall ? 1 : -1) * atr * _options.StopAtr;
                var end = Math.Min(bars.Length - 1, entryIndex + _options.ForwardMinutes - 1);
                var result = "NEITHER";
                var exit = (decimal)bars[end].Close;
                var exitTime = bars[end].OpenUtc + TimeSpan.FromMinutes(1);
                for (var i = entryIndex; i <= end; i++)
                {
                    var b = bars[i];
                    var hitTarget = isCall ? (decimal)b.High >= target : (decimal)b.Low <= target;
                    var hitStop = isCall ? (decimal)b.Low <= stop : (decimal)b.High >= stop;
                    // Conservative tie-break: if target and stop occur in the same 1m bar, count the stop first.
                    if (hitStop) { result = "STOP"; exit = stop; exitTime = b.OpenUtc + TimeSpan.FromMinutes(1); break; }
                    if (hitTarget) { result = "TARGET"; exit = target; exitTime = b.OpenUtc + TimeSpan.FromMinutes(1); break; }
                }
                var risk = atr * _options.StopAtr;
                var pnl = isCall ? exit - entry : entry - exit;
                var r = risk > 0 ? pnl / risk : 0;
                output.Add(new DiscoveryTrade(snap.Symbol, entryBar.OpenUtc + SessionClock.IstOffset, exitTime + SessionClock.IstOffset,
                    rule.Side, gapGroup, session, rule.Name, entry, exit, target, stop, result, r));
            }
        }
        return output;
    }

    private static bool Matches(DiscoverySnapshotRow s, DiscoveryRule r)
    {
        var call = r.Side == DiscoverySide.Call;
        if (call ? !(s.Ema20 > s.Ema50 && s.Ltp > s.Ema20) : !(s.Ema20 < s.Ema50 && s.Ltp < s.Ema20)) return false;
        if (r.RequireFiveMinuteTrend && (call ? !(s.Ema20_5m > s.Ema50_5m) : !(s.Ema20_5m < s.Ema50_5m))) return false;
        if (s.AtrPct < r.MinAtrPct) return false;
        if (r.RsiLow.HasValue || r.RsiHigh.HasValue)
        {
            if (!s.Rsi5m.HasValue || s.Rsi5m.Value < r.RsiLow.Value || s.Rsi5m.Value > r.RsiHigh.Value) return false;
        }
        if (r.PcrThreshold.HasValue)
        {
            var pcr = s.PcrFullChain ?? s.Pcr;
            if (call ? pcr < r.PcrThreshold.Value : pcr > r.PcrThreshold.Value) return false;
        }
        return true;
    }

    private static int UpperBound(Bar[] bars, DateTime time)
    {
        var lo = 0; var hi = bars.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (bars[mid].OpenUtc <= time) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
