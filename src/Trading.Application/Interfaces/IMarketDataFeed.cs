using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

public interface IMarketDataFeed
{
    event Func<string, Task>? TickReceived;

     Task StartAsync(
        IEnumerable<TradingInstrument> tradingInstruments,
        CancellationToken ct);
    Task StopAsync();
}
