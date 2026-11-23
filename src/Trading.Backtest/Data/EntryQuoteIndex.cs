using Trading.Backtest.Entry;

namespace Trading.Backtest.Data;

/// <summary>One real option quote the live engine recorded (read from the database, IST time).</summary>
public sealed class EntryQuoteRow
{
    public string Symbol { get; set; } = "";
    public DateTime TimeIst { get; set; }
    /// <summary>CE / PE as stored; CALL / PUT are also accepted.</summary>
    public string OptionType { get; set; } = "";
    public decimal Ltp { get; set; }
    public decimal Delta { get; set; }
    /// <summary>"Signals" or "IndicatorSnapshots" - where the quote came from.</summary>
    public string Origin { get; set; } = "";
}

public sealed record EntryQuote(DateTime TimeIst, double Ltp, double Delta, string Origin);

/// <summary>
/// Real entry premium and delta for days that have no OptionMinuteHistory (everything before 2026-10-07). The live engine
/// stored the option it would trade on every signal (Signals.OptionLtp / DeltaAtEntry) and on every post-gate block
/// (IndicatorSnapshots.BlockedOptionLtp / BlockedDelta). OptionChainData cannot be used: OptionChainWorker deletes its rows
/// after 2 hours.
///
/// No look-ahead: only a quote stamped at or before the entry time, and at most <see cref="MaxAge"/> older, is returned.
/// </summary>
public sealed class EntryQuoteIndex
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

    private readonly Dictionary<(string Symbol, bool IsCall), (DateTime[] Times, EntryQuoteRow[] Rows)> _series = new();

    public EntryQuoteIndex(IEnumerable<EntryQuoteRow> rows)
    {
        var valid = rows.Where(r => r.Ltp > 0 && r.Delta != 0 && IsCallType(r.OptionType) is not null).ToList();
        RowCount = valid.Count;
        foreach (var g in valid.GroupBy(r => (Symbol: r.Symbol.ToUpperInvariant(), IsCall: IsCallType(r.OptionType)!.Value)))
        {
            var ordered = g.OrderBy(r => r.TimeIst).ToArray();
            _series[g.Key] = (ordered.Select(r => r.TimeIst).ToArray(), ordered);
        }
    }

    public int RowCount { get; }

    /// <summary>The latest quote for the traded side at or before <paramref name="entryIst"/>, no older than MaxAge; else null.</summary>
    public EntryQuote? AtOrBefore(string symbol, TradeSide side, DateTime entryIst)
    {
        if (!_series.TryGetValue((symbol.ToUpperInvariant(), side == TradeSide.Call), out var s))
            return null;
        var idx = Array.BinarySearch(s.Times, entryIst);
        if (idx < 0) idx = ~idx - 1;
        else
            while (idx + 1 < s.Times.Length && s.Times[idx + 1] == entryIst) idx++;   // last row of an exact-time tie
        if (idx < 0 || entryIst - s.Times[idx] > MaxAge)
            return null;
        var r = s.Rows[idx];
        return new EntryQuote(r.TimeIst, (double)r.Ltp, Math.Abs((double)r.Delta), r.Origin);
    }

    private static bool? IsCallType(string? type) => type?.Trim().ToUpperInvariant() switch
    {
        "CE" or "CALL" => true,
        "PE" or "PUT" => false,
        _ => null,
    };
}
