using Microsoft.AspNetCore.SignalR;

namespace Trading.API.Hubs;

public class MarketHub : Hub
{
    private readonly ILogger<MarketHub> _logger;

    public MarketHub(ILogger<MarketHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeSymbol(
        string symbol)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            symbol);

        await Clients.Caller.SendAsync(
            "Subscribed",
            symbol);
    }

    public async Task UnSubscribeSymbol(
        string symbol)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            symbol);

        await Clients.Caller.SendAsync(
            "UnSubscribed",
            symbol);
    }
}