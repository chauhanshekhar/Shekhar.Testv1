using Microsoft.AspNetCore.SignalR;
using Trading.API.Hubs;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;

namespace Trading.API.Services;

public class MarketHubService : INotificationService
{
    private readonly IHubContext<MarketHub> _hub;

    public MarketHubService(IHubContext<MarketHub> hub)
    {
        _hub = hub;
    }

    public async Task SendSignalAsync(Signal signal)
    {
        await _hub.Clients.All.SendAsync("ReceiveSignal", signal);
    }

    public async Task BroadcastTickAsync(object tick)
    {
        await _hub.Clients.All.SendAsync("ReceiveTick", tick);
    }
}
