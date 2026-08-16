using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Tests.Fakes;

/// <summary>
/// Empty active-instrument list — the extension methods on IInstrumentCache (GetStrikeInterval,
/// GetExchange, GetLotSize) already fall back to sensible defaults (50 / "NSE" / 1) when no
/// instrument matches, which is all these tests need.
/// </summary>
public class FakeInstrumentCache : IInstrumentCache
{
    public IReadOnlyList<TradingInstrument> GetActive() => Array.Empty<TradingInstrument>();
    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
}
