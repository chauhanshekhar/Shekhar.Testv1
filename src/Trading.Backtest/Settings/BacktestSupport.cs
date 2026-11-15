using Microsoft.Extensions.Options;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Backtest.Settings;

/// <summary>Fixed-value IOptionsMonitor so the real SignalEngineService / SystemSettingsService can be constructed offline.</summary>
public sealed class StaticOptions<T> : IOptionsMonitor<T>
{
    public StaticOptions(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable OnChange(Action<T, string?> listener) => Noop.Instance;

    private sealed class Noop : IDisposable
    {
        public static readonly Noop Instance = new();
        public void Dispose() { }
    }
}

/// <summary>Instrument list loaded once from the database (strike interval, lot size); never reloads or writes.</summary>
public sealed class BacktestInstrumentCache : IInstrumentCache
{
    private readonly IReadOnlyList<TradingInstrument> _instruments;

    public BacktestInstrumentCache(IReadOnlyList<TradingInstrument> instruments) => _instruments = instruments;

    public IReadOnlyList<TradingInstrument> GetActive() => _instruments;

    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
}
