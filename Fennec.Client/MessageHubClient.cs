using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Fennec.Client;

public enum HubConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}

public interface IMessageHubClient : IAsyncDisposable
{
    Task ConnectAsync(string baseUrl, string token);
    Task SubscribeToChannelAsync(Guid serverId, Guid channelId);
    Task UnsubscribeFromChannelAsync(Guid serverId, Guid channelId);
    Task SubscribeToServerAsync(Guid serverId);
    Task UnsubscribeFromServerAsync(Guid serverId);
    Task<Dictionary<Guid, List<VoiceParticipantDto>>> GetServerVoiceStateAsync(Guid serverId, string instanceUrl);
    Task DisconnectAsync();
    event Action<Guid, Guid, MessageReceivedDto>? MessageReceived;
    event Action? Reconnected;
    event Action<HubConnectionStatus>? ConnectionStateChanged;

    // Voice
    Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl);
    Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl);
    Task SendSdpOfferAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendSdpAnswerAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendIceCandidateAsync(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex);

    event Action<Guid, Guid, VoiceParticipantDto>? VoiceParticipantJoined;
    event Action<Guid, Guid, Guid>? VoiceParticipantLeft;
    event Action<Guid, Guid, Guid, string>? SdpOfferReceived;
    event Action<Guid, Guid, Guid, string>? SdpAnswerReceived;
    event Action<Guid, Guid, Guid, string, string?, int?>? IceCandidateReceived;
}

public class MessageHubClient(ILogger<MessageHubClient> logger) : IMessageHubClient
{
    private HubConnection? _connection;
    private TaskCompletionSource _connectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    public event Action<Guid, Guid, MessageReceivedDto>? MessageReceived;
    public event Action? Reconnected;
    public event Action<HubConnectionStatus>? ConnectionStateChanged;

    // Voice events
    public event Action<Guid, Guid, VoiceParticipantDto>? VoiceParticipantJoined;
    public event Action<Guid, Guid, Guid>? VoiceParticipantLeft;
    public event Action<Guid, Guid, Guid, string>? SdpOfferReceived;
    public event Action<Guid, Guid, Guid, string>? SdpAnswerReceived;
    public event Action<Guid, Guid, Guid, string, string?, int?>? IceCandidateReceived;

    public async Task ConnectAsync(string baseUrl, string token)
    {
        if (_connection is not null)
            await DisconnectAsync();

        baseUrl = UrlUtils.NormalizeBaseUrl(baseUrl);
        logger.LogInformation("SignalR: Connecting to {Url}/hubs/messages", baseUrl);
        ConnectionStateChanged?.Invoke(HubConnectionStatus.Connecting);

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/messages", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect(new ForeverRetryPolicy())
            .Build();

        _connection.On<Guid, Guid, MessageReceivedDto>("MessageReceived", (serverId, channelId, message) =>
        {
            logger.LogInformation("SignalR: Received message on server={ServerId} channel={ChannelId} messageId={MessageId} from={Author}",
                serverId, channelId, message.MessageId, message.AuthorName);
            MessageReceived?.Invoke(serverId, channelId, message);
        });

        _connection.On<Guid, Guid, VoiceParticipantDto>("VoiceParticipantJoined", (serverId, channelId, participant) =>
        {
            VoiceParticipantJoined?.Invoke(serverId, channelId, participant);
        });

        _connection.On<Guid, Guid, Guid>("VoiceParticipantLeft", (serverId, channelId, userId) =>
        {
            VoiceParticipantLeft?.Invoke(serverId, channelId, userId);
        });

        _connection.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", (serverId, channelId, fromUserId, sdp) =>
        {
            SdpOfferReceived?.Invoke(serverId, channelId, fromUserId, sdp);
        });

        _connection.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (serverId, channelId, fromUserId, sdp) =>
        {
            SdpAnswerReceived?.Invoke(serverId, channelId, fromUserId, sdp);
        });

        _connection.On<Guid, Guid, Guid, string, string?, int?>("ReceiveIceCandidate", (serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex) =>
        {
            IceCandidateReceived?.Invoke(serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex);
        });

        _connection.Reconnected += _ =>
        {
            logger.LogInformation("SignalR: Reconnected");
            _connectedTcs.TrySetResult();
            ConnectionStateChanged?.Invoke(HubConnectionStatus.Connected);
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Closed += ex =>
        {
            logger.LogWarning(ex, "SignalR: Connection closed");
            _connectedTcs.TrySetCanceled();
            _connectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ConnectionStateChanged?.Invoke(HubConnectionStatus.Disconnected);
            return Task.CompletedTask;
        };

        _connection.Reconnecting += ex =>
        {
            logger.LogWarning(ex, "SignalR: Reconnecting...");
            ConnectionStateChanged?.Invoke(HubConnectionStatus.Reconnecting);
            return Task.CompletedTask;
        };

        await _connection.StartAsync();
        logger.LogInformation("SignalR: Connected (state={State})", _connection.State);
        _connectedTcs.TrySetResult();
        ConnectionStateChanged?.Invoke(HubConnectionStatus.Connected);
    }

    public async Task SubscribeToChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            logger.LogInformation("SignalR: Waiting for connection before subscribing to server={ServerId} channel={ChannelId}", serverId, channelId);
            try
            {
                await _connectedTcs.Task.WaitAsync(ConnectTimeout);
            }
            catch (TimeoutException)
            {
                logger.LogWarning("SignalR: Timed out waiting for connection to subscribe");
                return;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("SignalR: Connection closed while waiting to subscribe");
                return;
            }
        }

        logger.LogInformation("SignalR: Subscribing to server={ServerId} channel={ChannelId}", serverId, channelId);
        await _connection!.InvokeAsync("SubscribeToChannel", serverId, channelId);
    }

    public async Task UnsubscribeFromChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            logger.LogInformation("SignalR: Waiting for connection before unsubscribing from server={ServerId} channel={ChannelId}", serverId, channelId);
            try
            {
                await _connectedTcs.Task.WaitAsync(ConnectTimeout);
            }
            catch (TimeoutException)
            {
                logger.LogWarning("SignalR: Timed out waiting for connection to unsubscribe");
                return;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("SignalR: Connection closed while waiting to unsubscribe");
                return;
            }
        }

        logger.LogInformation("SignalR: Unsubscribing from server={ServerId} channel={ChannelId}", serverId, channelId);
        await _connection!.InvokeAsync("UnsubscribeFromChannel", serverId, channelId);
    }

    public async Task SubscribeToServerAsync(Guid serverId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            try
            {
                await _connectedTcs.Task.WaitAsync(ConnectTimeout);
            }
            catch (TimeoutException) { return; }
            catch (OperationCanceledException) { return; }
        }

        logger.LogInformation("SignalR: Subscribing to server group server={ServerId}", serverId);
        await _connection!.InvokeAsync("SubscribeToServer", serverId);
    }

    public async Task UnsubscribeFromServerAsync(Guid serverId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("UnsubscribeFromServer", serverId);
    }

    public async Task<Dictionary<Guid, List<VoiceParticipantDto>>> GetServerVoiceStateAsync(Guid serverId, string instanceUrl)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return new();
        return await _connection.InvokeAsync<Dictionary<Guid, List<VoiceParticipantDto>>>("GetServerVoiceState", serverId, instanceUrl);
    }

    // Voice methods

    public async Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return [];
        return await _connection.InvokeAsync<List<VoiceParticipantDto>>("JoinVoiceChannel", serverId, channelId, instanceUrl);
    }

    public async Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("LeaveVoiceChannel", serverId, channelId, instanceUrl);
    }

    public async Task SendSdpOfferAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("SendSdpOffer", serverId, channelId, targetUserId, sdp);
    }

    public async Task SendSdpAnswerAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("SendSdpAnswer", serverId, channelId, targetUserId, sdp);
    }

    public async Task SendIceCandidateAsync(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("SendIceCandidate", serverId, channelId, targetUserId, candidate, sdpMid, sdpMLineIndex);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
            _connectedTcs.TrySetCanceled();
            _connectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ConnectionStateChanged?.Invoke(HubConnectionStatus.Disconnected);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

internal sealed class ForeverRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var attempt = retryContext.PreviousRetryCount;
        var delay = attempt switch
        {
            0 => 0,
            1 => 2,
            2 => 5,
            3 => 10,
            4 => 30,
            _ => 60,
        };
        return TimeSpan.FromSeconds(delay);
    }
}
