using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

public interface INotificationService
{
    Task SendSignalAsync(Signal signal);
    Task BroadcastTickAsync(object tick);
}
