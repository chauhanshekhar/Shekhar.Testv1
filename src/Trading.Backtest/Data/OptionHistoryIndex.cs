using Trading.Backtest.Entry;
using Trading.SignalEngine.Services;

namespace Trading.Backtest.Data;

/// <summary>
/// Read-only index over OptionMinuteHistory, the only real historical option price source (from 2026-10-07; earlier days
/// have none). Used for two things: the OI confirmation gate (ATM +/- 2 strikes, ChangeInOi and the last index move) and a
/// real premium path for the exit replay. Times are treated as IST (the application stamps its rows with IstClock);
/// the runner prints an alignment check against the index candles so a wrong assumption is visible.
/// </summary>
public sealed class OptionHistoryIndex
{
    private static readonly OiAnalysisService Oi = new();

    /// <summary>Live compares the current chain with the latest snapshot at least this many minutes older.</summary>
    private const int MinOiLookbackMinutes = 4;
    private static readonly TimeSpan MaxOiLookback = TimeSpan.FromMinutes(30);

    private readonly Dictionary<string, SymbolOptions> _bySymbol = new(StringComparer.OrdinalIgnoreCase);

    public OptionHistoryIndex(IEnumerable<OptionMinuteRow> rows)
    {
        foreach (var group in rows.GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase))
            _bySymbol[group.Key] = new SymbolOptions(group);
    }

    public int RowCount => _bySymbol.Values.Sum(s => s.RowCount);

    public bool HasData(string symbol, DateTime istTime) =>
        _bySymbol.TryGetValue(symbol, out var s) && s.RowsAtOrBefore(istTime, TimeSpan.FromMinutes(3)) is { Count: > 0 };

    /// <summary>
    /// True/false = the OI gate's verdict; null = no option history at that minute (not replayable).
    ///
    /// The OI change is the LIVE definition (OiConfirmationGate / MarketSnapshotBuilder.PrevOiByKey): per ATM +/- 2 strike,
    /// OpenInterest now minus OpenInterest in the latest earlier snapshot that is at least 4 minutes older (a strike missing
    /// from the earlier snapshot counts from 0, as live does). Only when there is no such earlier snapshot does it fall back
    /// to the stored ChangeInOi, again as live does. The stored ChangeInOi is 0 on every ATM +/- 2 row since 2026-10-07, so
    /// summing it can never confirm anything.
    /// </summary>
    public bool? OiConfirms(string symbol, TradeSide side, DateTime istTime, int strikeInterval, double lastMove)
    {
        if (!_bySymbol.TryGetValue(symbol, out var s))
            return null;
        var rows = s.RowsAtOrBefore(istTime, TimeSpan.FromMinutes(3));
        if (rows is null || rows.Count == 0)
            return null;

        var expiry = rows.Where(r => r.ExpiryDate.Date >= istTime.Date).Select(r => r.ExpiryDate.Date).DefaultIfEmpty().Min();
        var spot = rows.Select(r => r.UnderlyingLtp).FirstOrDefault(v => v > 0);
        if (spot <= 0)
            return null;

        var atm = Math.Round(spot / strikeInterval, MidpointRounding.AwayFromZero) * strikeInterval;
        var near = rows.Where(r => r.ExpiryDate.Date == expiry && Math.Abs(r.StrikePrice - atm) <= strikeInterval * 2).ToList();
        if (near.Count == 0)
            return null;

        var earlier = s.RowsAtOrBefore(rows[0].MinuteTime.AddMinutes(-MinOiLookbackMinutes), MaxOiLookback);
        var previousOi = earlier?.Where(r => r.ExpiryDate.Date == expiry)
            .ToDictionary(r => (r.StrikePrice, r.OptionType), r => r.OpenInterest);

        long OiChange(string type) => near.Where(r => r.OptionType == type).Sum(r =>
            previousOi is { Count: > 0 }
                ? r.OpenInterest - previousOi.GetValueOrDefault((r.StrikePrice, r.OptionType))
                : r.ChangeInOi);

        var ce = OiChange("CE");
        var pe = OiChange("PE");
        var move = (decimal)lastMove;

        return side == TradeSide.Call
            ? Oi.IsLongBuildUp(ce, move) || Oi.IsShortCovering(ce, move)
            : Oi.IsLongBuildUp(pe, -move) || Oi.IsShortCovering(pe, -move);
    }

    /// <summary>
    /// Real premium path for the ATM contract of the traded side: entry = last stored LTP at or before the entry time; one value per
    /// following 1m bar close (forward-filled, up to 10 minutes of gap). Null when the history cannot cover at least 80% of the path.
    /// </summary>
    public RealPremiumPath? TryPath(string symbol, TradeSide side, DateTime entryIst, IReadOnlyList<DateTime> barCloseIst)
    {
        if (!_bySymbol.TryGetValue(symbol, out var s) || barCloseIst.Count == 0)
            return null;
        var rows = s.RowsAtOrBefore(entryIst, TimeSpan.FromMinutes(2));
        if (rows is null || rows.Count == 0)
            return null;

        var spot = rows.Select(r => r.UnderlyingLtp).FirstOrDefault(v => v > 0);
        var type = side == TradeSide.Call ? "CE" : "PE";
        var expiry = rows.Where(r => r.ExpiryDate.Date >= entryIst.Date).Select(r => r.ExpiryDate.Date).DefaultIfEmpty().Min();
        var contract = rows.Where(r => r.OptionType == type && r.ExpiryDate.Date == expiry && r.Ltp > 0)
            .OrderBy(r => Math.Abs((double)(r.StrikePrice - spot))).FirstOrDefault();
        if (contract is null)
            return null;

        var series = s.Series(contract.TradingSymbol);
        if (series is null)
            return null;

        var entry = series.LastAtOrBefore(entryIst, TimeSpan.FromMinutes(2));
        if (entry is null || entry.Ltp <= 0)
            return null;

        var moves = new double[barCloseIst.Count];
        var covered = 0;
        for (var i = 0; i < barCloseIst.Count; i++)
        {
            var v = series.LastAtOrBefore(barCloseIst[i], TimeSpan.FromMinutes(10));
            if (v is null) { moves[i] = i > 0 ? moves[i - 1] : 0; continue; }
            moves[i] = (double)(v.Ltp - entry.Ltp);
            covered++;
        }
        if (covered < barCloseIst.Count * 0.8)
            return null;

        return new RealPremiumPath((double)entry.Ltp, Math.Abs((double)contract.Delta), moves, contract.TradingSymbol);
    }

    private sealed class SymbolOptions
    {
        private readonly DateTime[] _minutes;
        private readonly Dictionary<DateTime, List<OptionMinuteRow>> _byMinute;
        private readonly Dictionary<string, ContractSeries> _series;
        public int RowCount { get; }

        public SymbolOptions(IEnumerable<OptionMinuteRow> rows)
        {
            var list = rows.ToList();
            RowCount = list.Count;
            _byMinute = list.GroupBy(r => r.MinuteTime).ToDictionary(g => g.Key, g => g.ToList());
            _minutes = _byMinute.Keys.OrderBy(k => k).ToArray();
            _series = list.GroupBy(r => r.TradingSymbol).ToDictionary(g => g.Key, g => new ContractSeries(g));
        }

        public List<OptionMinuteRow>? RowsAtOrBefore(DateTime t, TimeSpan maxAge)
        {
            var idx = Array.BinarySearch(_minutes, t);
            if (idx < 0) idx = ~idx - 1;
            if (idx < 0 || t - _minutes[idx] > maxAge)
                return null;
            return _byMinute[_minutes[idx]];
        }

        public ContractSeries? Series(string tradingSymbol) => _series.GetValueOrDefault(tradingSymbol);
    }

    private sealed class ContractSeries
    {
        private readonly OptionMinuteRow[] _rows;
        private readonly DateTime[] _times;

        public ContractSeries(IEnumerable<OptionMinuteRow> rows)
        {
            _rows = rows.OrderBy(r => r.MinuteTime).ToArray();
            _times = _rows.Select(r => r.MinuteTime).ToArray();
        }

        public OptionMinuteRow? LastAtOrBefore(DateTime t, TimeSpan maxAge)
        {
            var idx = Array.BinarySearch(_times, t);
            if (idx < 0) idx = ~idx - 1;
            if (idx < 0 || t - _times[idx] > maxAge)
                return null;
            return _rows[idx];
        }
    }
}

/// <summary>Premium moves (entry = 0) at each following 1m close from stored option prices.</summary>
public sealed record RealPremiumPath(double EntryPremium, double Delta, double[] Moves, string TradingSymbol);
