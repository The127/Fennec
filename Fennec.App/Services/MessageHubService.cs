using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.Client;

namespace Fennec.App.Services;

public interface IMessageHubService
{
    Task ConnectAsync(string baseUrl, string token);
    Task SubscribeToChannelAsync(Guid serverId, Guid channelId);
    Task DisconnectAsync();
}

public class MessageHubService(IMessageHubClient hubClient, IMessenger messenger) : IMessageHubService
{
    private Guid? _currentServerId;
    private Guid? _currentChannelId;

    public async Task ConnectAsync(string baseUrl, string token)
    {
        hubClient.MessageReceived += OnMessageReceived;
        await hubClient.ConnectAsync(baseUrl, token);
    }

    public async Task SubscribeToChannelAsync(Guid serverId, Guid channelId)
    {
        if (_currentServerId is not null && _currentChannelId is not null)
            await hubClient.UnsubscribeFromChannelAsync(_currentServerId.Value, _currentChannelId.Value);

        _currentServerId = serverId;
        _currentChannelId = channelId;
        await hubClient.SubscribeToChannelAsync(serverId, channelId);
    }

    public async Task DisconnectAsync()
    {
        hubClient.MessageReceived -= OnMessageReceived;
        _currentServerId = null;
        _currentChannelId = null;
        await hubClient.DisconnectAsync();
    }

    private void OnMessageReceived(Guid serverId, Guid channelId, Fennec.Shared.Dtos.Server.MessageReceivedDto message)
    {
        messenger.Send(new ChannelMessageReceivedMessage(serverId, channelId, message));
    }
}
