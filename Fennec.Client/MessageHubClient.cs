using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.SignalR.Client;

namespace Fennec.Client;

public interface IMessageHubClient : IAsyncDisposable
{
    Task ConnectAsync(string baseUrl, string token);
    Task SubscribeToChannelAsync(Guid serverId, Guid channelId);
    Task UnsubscribeFromChannelAsync(Guid serverId, Guid channelId);
    Task DisconnectAsync();
    event Action<Guid, Guid, MessageReceivedDto>? MessageReceived;
    event Action? Reconnected;

    // Voice
    Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId);
    Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId);
    Task SendSdpOfferAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendSdpAnswerAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendIceCandidateAsync(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex);

    event Action<Guid, Guid, VoiceParticipantDto>? VoiceParticipantJoined;
    event Action<Guid, Guid, Guid>? VoiceParticipantLeft;
    event Action<Guid, Guid, Guid, string>? SdpOfferReceived;
    event Action<Guid, Guid, Guid, string>? SdpAnswerReceived;
    event Action<Guid, Guid, Guid, string, string?, int?>? IceCandidateReceived;
}

public class MessageHubClient : IMessageHubClient
{
    private HubConnection? _connection;

    public event Action<Guid, Guid, MessageReceivedDto>? MessageReceived;
    public event Action? Reconnected;

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

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/messages", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<Guid, Guid, MessageReceivedDto>("MessageReceived", (serverId, channelId, message) =>
        {
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
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };

        await _connection.StartAsync();
    }

    public async Task SubscribeToChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("SubscribeToChannel", serverId, channelId);
    }

    public async Task UnsubscribeFromChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("UnsubscribeFromChannel", serverId, channelId);
    }

    // Voice methods

    public async Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return [];
        return await _connection.InvokeAsync<List<VoiceParticipantDto>>("JoinVoiceChannel", serverId, channelId);
    }

    public async Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("LeaveVoiceChannel", serverId, channelId);
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
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
