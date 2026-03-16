using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.Client;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

public interface IVoiceHubService
{
    Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl);
    Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl);
    Task SendSdpOfferAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendSdpAnswerAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp);
    Task SendIceCandidateAsync(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex);
    Task SetMuteStateAsync(Guid serverId, Guid channelId, bool isMuted);
    Task SetDeafenStateAsync(Guid serverId, Guid channelId, bool isDeafened);
    Task SetSpeakingStateAsync(Guid serverId, Guid channelId, bool isSpeaking);
    Task StartScreenShareAsync(Guid serverId, Guid channelId);
    Task StopScreenShareAsync(Guid serverId, Guid channelId);
    Task WatchScreenShareAsync(Guid serverId, Guid channelId, Guid sharerUserId);
    Task UnwatchScreenShareAsync(Guid serverId, Guid channelId, Guid sharerUserId);

    event Action<Guid, Guid, Guid, string>? SdpOfferReceived;
    event Action<Guid, Guid, Guid, string>? SdpAnswerReceived;
    event Action<Guid, Guid, Guid, string, string?, int?>? IceCandidateReceived;

    void Initialize();
}

public class VoiceHubService : IVoiceHubService
{
    private readonly IMessageHubClient _hubClient;
    private readonly IMessenger _messenger;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<VoiceHubService> _logger;

    private HubConnection? _directConnection;
    private bool _usingDirect;

    public event Action<Guid, Guid, Guid, string>? SdpOfferReceived;
    public event Action<Guid, Guid, Guid, string>? SdpAnswerReceived;
    public event Action<Guid, Guid, Guid, string, string?, int?>? IceCandidateReceived;

    public VoiceHubService(IMessageHubClient hubClient, IMessenger messenger, ITokenStore tokenStore, ILogger<VoiceHubService> logger)
    {
        _hubClient = hubClient;
        _messenger = messenger;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public void Initialize()
    {
        _hubClient.VoiceParticipantJoined += (serverId, channelId, participant) =>
        {
            _messenger.Send(new VoiceParticipantJoinedMessage(serverId, channelId, participant.UserId, participant.Username, participant.InstanceUrl));
        };

        _hubClient.VoiceParticipantLeft += (serverId, channelId, userId) =>
        {
            _messenger.Send(new VoiceParticipantLeftMessage(serverId, channelId, userId));
        };

        _hubClient.SdpOfferReceived += (serverId, channelId, fromUserId, sdp) =>
        {
            SdpOfferReceived?.Invoke(serverId, channelId, fromUserId, sdp);
        };

        _hubClient.SdpAnswerReceived += (serverId, channelId, fromUserId, sdp) =>
        {
            SdpAnswerReceived?.Invoke(serverId, channelId, fromUserId, sdp);
        };

        _hubClient.IceCandidateReceived += (serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex) =>
        {
            IceCandidateReceived?.Invoke(serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex);
        };

        _hubClient.VoiceMuteStateChanged += (serverId, channelId, userId, isMuted) =>
        {
            _messenger.Send(new VoiceMuteStateChangedMessage(serverId, channelId, userId, isMuted));
        };

        _hubClient.VoiceDeafenStateChanged += (serverId, channelId, userId, isDeafened) =>
        {
            _messenger.Send(new VoiceDeafenStateChangedMessage(serverId, channelId, userId, isDeafened));
        };

        _hubClient.VoiceSpeakingStateChanged += (serverId, channelId, userId, isSpeaking) =>
        {
            _messenger.Send(new VoiceSpeakingChangedMessage(serverId, channelId, userId, isSpeaking));
        };

        _hubClient.ScreenShareStarted += (serverId, channelId, userId, username, instanceUrl) =>
        {
            _messenger.Send(new ScreenShareStartedMessage(serverId, channelId, userId, username, instanceUrl));
        };

        _hubClient.ScreenShareStopped += (serverId, channelId, userId) =>
        {
            _messenger.Send(new ScreenShareStoppedMessage(serverId, channelId, userId));
        };

        _hubClient.ScreenShareWatcherAdded += (serverId, channelId, watcherUserId) =>
        {
            _messenger.Send(new ScreenShareWatcherAddedMessage(serverId, channelId, watcherUserId));
        };

        _hubClient.ScreenShareWatcherRemoved += (serverId, channelId, watcherUserId) =>
        {
            _messenger.Send(new ScreenShareWatcherRemovedMessage(serverId, channelId, watcherUserId));
        };
    }

    private bool IsRemoteInstance(string instanceUrl)
    {
        var homeUrl = _tokenStore.HomeUrl;
        if (homeUrl is null) return false;
        return !string.Equals(
            UrlUtils.NormalizeBaseUrl(instanceUrl),
            UrlUtils.NormalizeBaseUrl(homeUrl),
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<VoiceParticipantDto>> JoinVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl)
    {
        if (!IsRemoteInstance(instanceUrl))
        {
            return await _hubClient.JoinVoiceChannelAsync(serverId, channelId, instanceUrl);
        }

        // Remote instance — open a direct SignalR connection to the hosting server
        var normalizedUrl = UrlUtils.NormalizeBaseUrl(instanceUrl);
        var jwt = _tokenStore.GetPublicToken(normalizedUrl);
        if (jwt is null)
        {
            _logger.LogWarning("No public token cached for {InstanceUrl}, falling back to home hub", normalizedUrl);
            return await _hubClient.JoinVoiceChannelAsync(serverId, channelId, instanceUrl);
        }

        _logger.LogInformation("Voice: Opening direct SignalR connection to {InstanceUrl}", normalizedUrl);

        _directConnection = new HubConnectionBuilder()
            .WithUrl($"{normalizedUrl}/hubs/messages", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(jwt);
                options.HttpMessageHandlerFactory = _ => Ipv4HttpHandler.Create();
            })
            .WithAutomaticReconnect(new ForeverRetryPolicy())
            .Build();

        // Register voice event handlers on the direct connection
        _directConnection.On<Guid, Guid, VoiceParticipantDto>("VoiceParticipantJoined", (sid, cid, participant) =>
        {
            _messenger.Send(new VoiceParticipantJoinedMessage(sid, cid, participant.UserId, participant.Username, participant.InstanceUrl));
        });

        _directConnection.On<Guid, Guid, Guid>("VoiceParticipantLeft", (sid, cid, userId) =>
        {
            _messenger.Send(new VoiceParticipantLeftMessage(sid, cid, userId));
        });

        _directConnection.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", (sid, cid, fromUserId, sdp) =>
        {
            SdpOfferReceived?.Invoke(sid, cid, fromUserId, sdp);
        });

        _directConnection.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (sid, cid, fromUserId, sdp) =>
        {
            SdpAnswerReceived?.Invoke(sid, cid, fromUserId, sdp);
        });

        _directConnection.On<Guid, Guid, Guid, string, string?, int?>("ReceiveIceCandidate", (sid, cid, fromUserId, candidate, sdpMid, sdpMLineIndex) =>
        {
            IceCandidateReceived?.Invoke(sid, cid, fromUserId, candidate, sdpMid, sdpMLineIndex);
        });

        _directConnection.On<Guid, Guid, Guid, bool>("VoiceMuteStateChanged", (sid, cid, userId, isMuted) =>
        {
            _messenger.Send(new VoiceMuteStateChangedMessage(sid, cid, userId, isMuted));
        });

        _directConnection.On<Guid, Guid, Guid, bool>("VoiceDeafenStateChanged", (sid, cid, userId, isDeafened) =>
        {
            _messenger.Send(new VoiceDeafenStateChangedMessage(sid, cid, userId, isDeafened));
        });

        _directConnection.On<Guid, Guid, Guid, bool>("VoiceSpeakingStateChanged", (sid, cid, userId, isSpeaking) =>
        {
            _messenger.Send(new VoiceSpeakingChangedMessage(sid, cid, userId, isSpeaking));
        });

        _directConnection.On<Guid, Guid, Guid, string, string?>("ScreenShareStarted", (sid, cid, userId, username, instanceUrl) =>
        {
            _messenger.Send(new ScreenShareStartedMessage(sid, cid, userId, username, instanceUrl));
        });

        _directConnection.On<Guid, Guid, Guid>("ScreenShareStopped", (sid, cid, userId) =>
        {
            _messenger.Send(new ScreenShareStoppedMessage(sid, cid, userId));
        });

        _directConnection.On<Guid, Guid, Guid>("ScreenShareWatcherAdded", (sid, cid, watcherUserId) =>
        {
            _messenger.Send(new ScreenShareWatcherAddedMessage(sid, cid, watcherUserId));
        });

        _directConnection.On<Guid, Guid, Guid>("ScreenShareWatcherRemoved", (sid, cid, watcherUserId) =>
        {
            _messenger.Send(new ScreenShareWatcherRemovedMessage(sid, cid, watcherUserId));
        });

        _directConnection.Closed += ex =>
        {
            _logger.LogWarning(ex, "Voice: Direct connection to {InstanceUrl} closed", normalizedUrl);
            return Task.CompletedTask;
        };

        _directConnection.Reconnecting += ex =>
        {
            _logger.LogWarning(ex, "Voice: Direct connection to {InstanceUrl} reconnecting...", normalizedUrl);
            return Task.CompletedTask;
        };

        _directConnection.Reconnected += _ =>
        {
            _logger.LogInformation("Voice: Direct connection to {InstanceUrl} reconnected", normalizedUrl);
            return Task.CompletedTask;
        };

        await _directConnection.StartAsync();
        _usingDirect = true;
        _logger.LogInformation("Voice: Direct connection established to {InstanceUrl}", normalizedUrl);

        return await _directConnection.InvokeAsync<List<VoiceParticipantDto>>("JoinVoiceChannel", serverId, channelId, instanceUrl);
    }

    public async Task LeaveVoiceChannelAsync(Guid serverId, Guid channelId, string instanceUrl)
    {
        if (_usingDirect && _directConnection is not null)
        {
            if (_directConnection.State == HubConnectionState.Connected)
                await _directConnection.InvokeAsync("LeaveVoiceChannel", serverId, channelId, instanceUrl);

            await _directConnection.DisposeAsync();
            _directConnection = null;
            _usingDirect = false;
            _logger.LogInformation("Voice: Direct connection closed");
            return;
        }

        await _hubClient.LeaveVoiceChannelAsync(serverId, channelId, instanceUrl);
    }

    public async Task SendSdpOfferAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("SendSdpOffer", serverId, channelId, targetUserId, sdp);
        else
            await _hubClient.SendSdpOfferAsync(serverId, channelId, targetUserId, sdp);
    }

    public async Task SendSdpAnswerAsync(Guid serverId, Guid channelId, Guid targetUserId, string sdp)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("SendSdpAnswer", serverId, channelId, targetUserId, sdp);
        else
            await _hubClient.SendSdpAnswerAsync(serverId, channelId, targetUserId, sdp);
    }

    public async Task SendIceCandidateAsync(Guid serverId, Guid channelId, Guid targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("SendIceCandidate", serverId, channelId, targetUserId, candidate, sdpMid, sdpMLineIndex);
        else
            await _hubClient.SendIceCandidateAsync(serverId, channelId, targetUserId, candidate, sdpMid, sdpMLineIndex);
    }

    public async Task SetMuteStateAsync(Guid serverId, Guid channelId, bool isMuted)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("SetMuteState", serverId, channelId, isMuted);
        else
            await _hubClient.SetMuteStateAsync(serverId, channelId, isMuted);
    }

    public async Task SetDeafenStateAsync(Guid serverId, Guid channelId, bool isDeafened)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("SetDeafenState", serverId, channelId, isDeafened);
        else
            await _hubClient.SetDeafenStateAsync(serverId, channelId, isDeafened);
    }

    public async Task SetSpeakingStateAsync(Guid serverId, Guid channelId, bool isSpeaking)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("SetSpeakingState", serverId, channelId, isSpeaking);
        else
            await _hubClient.SetSpeakingStateAsync(serverId, channelId, isSpeaking);
    }

    public async Task StartScreenShareAsync(Guid serverId, Guid channelId)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("StartScreenShare", serverId, channelId);
        else
            await _hubClient.StartScreenShareAsync(serverId, channelId);
    }

    public async Task StopScreenShareAsync(Guid serverId, Guid channelId)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("StopScreenShare", serverId, channelId);
        else
            await _hubClient.StopScreenShareAsync(serverId, channelId);
    }

    public async Task WatchScreenShareAsync(Guid serverId, Guid channelId, Guid sharerUserId)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("WatchScreenShare", serverId, channelId, sharerUserId);
        else
            await _hubClient.WatchScreenShareAsync(serverId, channelId, sharerUserId);
    }

    public async Task UnwatchScreenShareAsync(Guid serverId, Guid channelId, Guid sharerUserId)
    {
        if (_usingDirect && _directConnection?.State == HubConnectionState.Connected)
            await _directConnection.InvokeAsync("UnwatchScreenShare", serverId, channelId, sharerUserId);
        else
            await _hubClient.UnwatchScreenShareAsync(serverId, channelId, sharerUserId);
    }
}
