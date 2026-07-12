using Trading.Application.Interfaces;
using Trading.Domain.Entities;

namespace Trading.Worker;

// Worker has no SignalR hub — signals are persisted to DB by SignalWorker.
// The API's MarketHubService broadcasts to clients; this no-op satisfies the
// INotificationService dependency without pulling in ASP.NET SignalR.
internal sealed class NullNotificationService : INotificationService
{
    public Task SendSignalAsync(Signal signal) => Task.CompletedTask;
    public Task BroadcastTickAsync(object tick) => Task.CompletedTask;
}
