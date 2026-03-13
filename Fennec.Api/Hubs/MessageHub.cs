using Fennec.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Fennec.Api.Hubs;

public class MessageHub : Hub
{
    public async Task SubscribeToChannel(Guid serverId, Guid channelId)
    {
        // TODO: check if user is on the server and has access to the channel
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{serverId}-{channelId}");
    }
}

public interface IMessageEventService
{
    Task NotifyMessageReceived(Guid serverId, Guid channelId, ChannelMessage message,
        CancellationToken cancellationToken);
}

public class MessageEventService(
    IHubContext<MessageHub> messageHubContext    
) : IMessageEventService
{
    public Task NotifyMessageReceived(Guid serverId, Guid channelId, ChannelMessage message, CancellationToken cancellationToken)
    {
        return messageHubContext.Clients.Group($"{serverId}-{channelId}")
            .SendAsync("MessageReceived", serverId, channelId, message, cancellationToken: cancellationToken);

    }
}