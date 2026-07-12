using Microsoft.AspNetCore.SignalR;

namespace Trading.API.Hubs;

public class SignalHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "signals");
        await base.OnConnectedAsync();
    }
}
