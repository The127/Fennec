using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.Client;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

public interface IMessageHubService
{
    Task ConnectAsync(string baseUrl, string token);
    Task SubscribeToChannelAsync(Guid serverId, Guid channelId);
    Task DisconnectAsync();
    Guid? CurrentServerId { get; }
    Guid? CurrentChannelId { get; }
}

public class MessageHubService(IMessageHubClient hubClient, IMessenger messenger, ILogger<MessageHubService> logger) : IMessageHubService
{
    public Guid? CurrentServerId => _currentServerId;
    public Guid? CurrentChannelId => _currentChannelId;

    private Guid? _currentServerId;
    private Guid? _currentChannelId;

    public async Task ConnectAsync(string baseUrl, string token)
    {
        logger.LogInformation("MessageHubService: Connecting to {BaseUrl}", baseUrl);
        hubClient.MessageReceived += OnMessageReceived;
        hubClient.Reconnected += OnReconnected;
        hubClient.ConnectionStateChanged += OnConnectionStateChanged;
        await hubClient.ConnectAsync(baseUrl, token);
    }

    public async Task SubscribeToChannelAsync(Guid serverId, Guid channelId)
    {
        if (_currentServerId is not null && _currentChannelId is not null)
        {
            logger.LogInformation("MessageHubService: Unsubscribing from previous server={ServerId} channel={ChannelId}",
                _currentServerId, _currentChannelId);
            await hubClient.UnsubscribeFromChannelAsync(_currentServerId.Value, _currentChannelId.Value);
        }

        _currentServerId = serverId;
        _currentChannelId = channelId;
        logger.LogInformation("MessageHubService: Subscribing to server={ServerId} channel={ChannelId}", serverId, channelId);
        await hubClient.SubscribeToChannelAsync(serverId, channelId);
    }

    public async Task DisconnectAsync()
    {
        logger.LogInformation("MessageHubService: Disconnecting");
        hubClient.MessageReceived -= OnMessageReceived;
        hubClient.Reconnected -= OnReconnected;
        hubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _currentServerId = null;
        _currentChannelId = null;
        await hubClient.DisconnectAsync();
    }

    private void OnMessageReceived(Guid serverId, Guid channelId, Fennec.Shared.Dtos.Server.MessageReceivedDto message)
    {
        logger.LogInformation("MessageHubService: Message received on server={ServerId} channel={ChannelId} messageId={MessageId}",
            serverId, channelId, message.MessageId);
        messenger.Send(new ChannelMessageReceivedMessage(serverId, channelId, message));
    }

    private void OnReconnected()
    {
        logger.LogInformation("MessageHubService: Reconnected, re-subscribing to server={ServerId} channel={ChannelId}",
            _currentServerId, _currentChannelId);
        if (_currentServerId is not null && _currentChannelId is not null)
            _ = hubClient.SubscribeToChannelAsync(_currentServerId.Value, _currentChannelId.Value);
    }

    private void OnConnectionStateChanged(HubConnectionStatus status)
    {
        messenger.Send(new HubConnectionStateChangedMessage(status));
    }
}
