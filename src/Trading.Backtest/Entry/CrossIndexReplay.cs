using Trading.Backtest.Data;
using Trading.Backtest.Indicators;
using Trading.Domain.Entities;
using Trading.Worker.SignalGates;

namespace Trading.Backtest.Entry;

/// <summary>
/// Replays CrossIndexAgreementGate. The live gate reads the OTHER active indices' closed 1m bars, so the replay needs every symbol's
/// series at one decision time. It calls the gate's own SignedMove / Evaluate on bars closed at that time (open &lt;= now - 1 minute),
/// so the closed-bar, staleness and "not enough bars" rules are the live ones, not copies.
/// </summary>
internal sealed class CrossIndexReplay
{
    private readonly Dictionary<string, (Bar[] Bars, DateTime[] Times)> _series = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyCollection<string> _activeSymbols;

    public CrossIndexReplay(IEnumerable<(string Symbol, Bar[] Bars)> series, IReadOnlyCollection<string> activeSymbols)
    {
        _activeSymbols = activeSymbols;
        foreach (var (symbol, bars) in series)
        {
            var ordered = bars.OrderBy(b => b.OpenUtc).ToArray();
            _series[symbol] = (ordered, ordered.Select(b => b.OpenUtc).ToArray());
        }
    }

    /// <summary>The block reason, or null to proceed. Mirrors CrossIndexAgreementGate.BlockAsync (HeroZero never occurs in the replay).</summary>
    public string? Block(string symbol, string signalType, DateTime nowUtc, int side, int lookback, int minAgree)
    {
        if (!CrossIndexAgreementGate.AppliesTo(side, signalType))
            return null;

        lookback = Math.Max(2, lookback);
        minAgree = Math.Max(1, minAgree);

        var moves = new List<(string Symbol, decimal? Move)>();
        foreach (var other in _activeSymbols.Where(s => !s.Equals(symbol, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_series.TryGetValue(other, out var s))
            {
                moves.Add((other, null));       // no data for that index = a feed gap, as live
                continue;
            }

            // live reads the newest lookback + 5 rows; the closed ones are those opened at least a minute ago
            var n = MarketSeriesBuilder.UpperBound(s.Times, nowUtc - TimeSpan.FromMinutes(1));
            var from = Math.Max(0, n - (lookback + 5));
            var candles = new List<Candle>(n - from);
            for (var i = from; i < n; i++)
                candles.Add(MarketSeriesBuilder.ToCandle(other, s.Bars[i]));

            moves.Add((other, CrossIndexAgreementGate.SignedMove(candles, lookback, nowUtc)));
        }

        var reason = CrossIndexAgreementGate.Evaluate(signalType, moves, minAgree);
        return reason is null ? null : $"{reason} ({symbol})";
    }
}
