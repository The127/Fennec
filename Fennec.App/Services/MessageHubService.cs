using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.Client;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

public interface IMessageHubService
{
    Task ConnectAsync(string baseUrl, string token);
    Task SubscribeToChannelAsync(Guid serverId, Guid channelId);
    Task SubscribeToServerAsync(Guid serverId);
    Task UnsubscribeFromServerAsync(Guid serverId);
    Task<Dictionary<Guid, List<Fennec.Shared.Dtos.Voice.VoiceParticipantDto>>> GetServerVoiceStateAsync(Guid serverId, string instanceUrl);
    Task<List<Fennec.Shared.Dtos.Server.ServerPresenceEntryDto>> GetServerPresenceAsync(Guid serverId);
    Task DisconnectAsync();
    Guid? CurrentServerId { get; }
    Guid? CurrentChannelId { get; }
    HubConnectionStatus CurrentStatus { get; }
}

public class MessageHubService(IMessageHubClient hubClient, IMessenger messenger, ILogger<MessageHubService> logger) : IMessageHubService
{
    public Guid? CurrentServerId => _currentServerId;
    public Guid? CurrentChannelId => _currentChannelId;
    public HubConnectionStatus CurrentStatus { get; private set; } = HubConnectionStatus.Disconnected;

    private Guid? _currentServerId;
    private Guid? _currentChannelId;
    private readonly HashSet<Guid> _subscribedServerIds = [];

    public async Task ConnectAsync(string baseUrl, string token)
    {
        logger.LogInformation("MessageHubService: Connecting to {BaseUrl}", baseUrl);
        hubClient.MessageReceived += OnMessageReceived;
        hubClient.Reconnected += OnReconnected;
        hubClient.ConnectionStateChanged += OnConnectionStateChanged;
        hubClient.UserOnline += OnUserOnline;
        hubClient.UserOffline += OnUserOffline;
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

    public async Task SubscribeToServerAsync(Guid serverId)
    {
        _subscribedServerIds.Add(serverId);
        await hubClient.SubscribeToServerAsync(serverId);
    }

    public async Task UnsubscribeFromServerAsync(Guid serverId)
    {
        _subscribedServerIds.Remove(serverId);
        await hubClient.UnsubscribeFromServerAsync(serverId);
    }

    public Task<Dictionary<Guid, List<Fennec.Shared.Dtos.Voice.VoiceParticipantDto>>> GetServerVoiceStateAsync(Guid serverId, string instanceUrl)
        => hubClient.GetServerVoiceStateAsync(serverId, instanceUrl);

    public Task<List<Fennec.Shared.Dtos.Server.ServerPresenceEntryDto>> GetServerPresenceAsync(Guid serverId)
        => hubClient.GetServerPresenceAsync(serverId);

    public async Task DisconnectAsync()
    {
        logger.LogInformation("MessageHubService: Disconnecting");
        hubClient.MessageReceived -= OnMessageReceived;
        hubClient.Reconnected -= OnReconnected;
        hubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        hubClient.UserOnline -= OnUserOnline;
        hubClient.UserOffline -= OnUserOffline;
        _currentServerId = null;
        _currentChannelId = null;
        await hubClient.DisconnectAsync();
    }

    private void OnUserOnline(Guid serverId, Guid userId, string username, string? instanceUrl)
    {
        messenger.Send(new UserOnlineMessage(serverId, userId, username, instanceUrl));
    }

    private void OnUserOffline(Guid serverId, Guid userId)
    {
        messenger.Send(new UserOfflineMessage(serverId, userId));
    }

    private void OnMessageReceived(Guid serverId, Guid channelId, Fennec.Shared.Dtos.Server.MessageReceivedDto message)
    {
        logger.LogInformation("MessageHubService: Message received on server={ServerId} channel={ChannelId} messageId={MessageId}",
            serverId, channelId, message.MessageId);
        messenger.Send(new ChannelMessageReceivedMessage(serverId, channelId, message));
    }

    private void OnReconnected()
    {
        // Re-subscription is handled by OnConnectionStateChanged when status becomes Connected.
    }

    private void OnConnectionStateChanged(HubConnectionStatus status)
    {
        CurrentStatus = status;
        messenger.Send(new HubConnectionStateChangedMessage(status));

        if (status == HubConnectionStatus.Connected)
        {
            // Re-subscribe to channel if we had one
            if (_currentServerId is not null && _currentChannelId is not null)
            {
                logger.LogInformation("MessageHubService: Connection established, subscribing to server={ServerId} channel={ChannelId}",
                    _currentServerId, _currentChannelId);
                _ = hubClient.SubscribeToChannelAsync(_currentServerId.Value, _currentChannelId.Value);
            }

            // Re-subscribe to all server groups
            foreach (var serverId in _subscribedServerIds)
            {
                logger.LogInformation("MessageHubService: Re-subscribing to server group server={ServerId}", serverId);
                _ = hubClient.SubscribeToServerAsync(serverId);
            }
        }
    }
}
