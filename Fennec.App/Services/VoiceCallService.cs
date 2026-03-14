using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace Fennec.App.Services;

public interface IVoiceCallService
{
    Task JoinAsync(Guid serverId, Guid channelId, string instanceUrl);
    Task LeaveAsync();
    void SetMuted(bool muted);
    void SetDeafened(bool deafened);
    bool IsConnected { get; }
    Guid? CurrentServerId { get; }
    Guid? CurrentChannelId { get; }
    bool IsMuted { get; }
    bool IsDeafened { get; }
}

public class VoiceCallService : IVoiceCallService, IDisposable
{
    private readonly IVoiceHubService _voiceHub;
    private readonly IMessenger _messenger;
    private readonly ILogger<VoiceCallService> _logger;
    private readonly ISettingsStore _settingsStore;

    private readonly Dictionary<Guid, RTCPeerConnection> _peers = new();
    private PortAudioEndPoint? _audioEndPoint;

    public bool IsConnected { get; private set; }
    public Guid? CurrentServerId { get; private set; }
    public Guid? CurrentChannelId { get; private set; }
    public string? CurrentInstanceUrl { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsDeafened { get; private set; }

    private static readonly RTCIceServer[] StunServers =
    [
        new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
    ];

    public VoiceCallService(IVoiceHubService voiceHub, IMessenger messenger, ILogger<VoiceCallService> logger, ISettingsStore settingsStore)
    {
        _voiceHub = voiceHub;
        _messenger = messenger;
        _logger = logger;
        _settingsStore = settingsStore;

        _voiceHub.SdpOfferReceived += OnSdpOfferReceived;
        _voiceHub.SdpAnswerReceived += OnSdpAnswerReceived;
        _voiceHub.IceCandidateReceived += OnIceCandidateReceived;
    }

    public async Task JoinAsync(Guid serverId, Guid channelId, string instanceUrl)
    {
        if (IsConnected)
            await LeaveAsync();

        CurrentServerId = serverId;
        CurrentChannelId = channelId;
        CurrentInstanceUrl = instanceUrl;

        await TryInitAudioEndPointAsync();

        var participants = await _voiceHub.JoinVoiceChannelAsync(serverId, channelId, instanceUrl);
        IsConnected = true;
        _messenger.Send(new VoiceStateChangedMessage(true, serverId, channelId));

        // Notify UI about all current participants (including self)
        foreach (var participant in participants)
        {
            _messenger.Send(new VoiceParticipantJoinedMessage(serverId, channelId, participant.UserId, participant.Username, participant.InstanceUrl));
        }

        // Create peer connections to all existing participants except self (joiner offers)
        foreach (var participant in participants)
        {
            await CreatePeerAndOffer(participant.UserId);
        }
    }

    public async Task LeaveAsync()
    {
        if (!IsConnected)
            return;

        var serverId = CurrentServerId!.Value;
        var channelId = CurrentChannelId!.Value;

        try
        {
            await _voiceHub.LeaveVoiceChannelAsync(serverId, channelId, CurrentInstanceUrl!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error leaving voice channel");
        }

        CleanupPeers();
        CleanupAudio();

        IsConnected = false;
        CurrentServerId = null;
        CurrentChannelId = null;
        CurrentInstanceUrl = null;
        IsMuted = false;
        IsDeafened = false;

        _messenger.Send(new VoiceStateChangedMessage(false, null, null));
    }

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        if (_audioEndPoint is not null)
        {
            if (muted)
                _ = _audioEndPoint.PauseAudio();
            else
                _ = _audioEndPoint.ResumeAudio();
        }
    }

    public void SetDeafened(bool deafened)
    {
        IsDeafened = deafened;
        // When deafened, also mute
        if (deafened && !IsMuted)
            SetMuted(true);
    }

    private async Task TryInitAudioEndPointAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            var inputIndex = PortAudioEndPoint.FindDeviceByName(settings.InputDeviceName, settings.AudioHostApi);
            var outputIndex = PortAudioEndPoint.FindDeviceByName(settings.OutputDeviceName, settings.AudioHostApi);

            _audioEndPoint = new PortAudioEndPoint(_logger, inputIndex, outputIndex);
            _audioEndPoint.StartAudio();
            _audioEndPoint.StartAudioSink();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize PortAudio audio endpoint");
            _audioEndPoint = null;
        }
    }

    private void CleanupAudio()
    {
        if (_audioEndPoint is not null)
        {
            _audioEndPoint.Dispose();
            _audioEndPoint = null;
        }
    }

    private async Task CreatePeerAndOffer(Guid remoteUserId)
    {
        var pc = CreatePeerConnection(remoteUserId);
        _peers[remoteUserId] = pc;

        var offer = pc.createOffer();
        await pc.setLocalDescription(offer);

        await _voiceHub.SendSdpOfferAsync(
            CurrentServerId!.Value, CurrentChannelId!.Value,
            remoteUserId, offer.sdp);
    }

    private RTCPeerConnection CreatePeerConnection(Guid remoteUserId)
    {
        var config = new RTCConfiguration
        {
            iceServers = [.. StunServers]
        };

        var pc = new RTCPeerConnection(config);

        // Add audio track with Opus codec
        var audioFormat = new AudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2);
        var audioTrack = new MediaStreamTrack(audioFormat, MediaStreamStatusEnum.SendRecv);
        pc.addTrack(audioTrack);

        // Wire up audio source to send encoded samples
        if (_audioEndPoint is not null)
        {
            _audioEndPoint.OnAudioSourceEncodedSample += (durationRtpUnits, sample) =>
            {
                if (!IsMuted)
                    pc.SendAudio(durationRtpUnits, sample);
            };
        }

        // Handle incoming audio
        pc.OnRtpPacketReceived += (ep, media, pkt) =>
        {
            if (media == SDPMediaTypesEnum.audio && !IsDeafened && _audioEndPoint is not null)
            {
                _audioEndPoint.GotAudioRtp(ep, pkt.Header.SyncSource, pkt.Header.SequenceNumber,
                    pkt.Header.Timestamp, pkt.Header.PayloadType, pkt.Header.MarkerBit == 1, pkt.Payload);
            }
        };

        pc.onicecandidate += candidate =>
        {
            _ = _voiceHub.SendIceCandidateAsync(
                CurrentServerId!.Value, CurrentChannelId!.Value,
                remoteUserId, candidate.candidate, candidate.sdpMid, (int?)candidate.sdpMLineIndex);
        };

        pc.onconnectionstatechange += state =>
        {
            _logger.LogDebug("Peer {RemoteUser} connection state: {State}", remoteUserId, state);
            if (state is RTCPeerConnectionState.failed or RTCPeerConnectionState.disconnected or RTCPeerConnectionState.closed)
            {
                if (_peers.Remove(remoteUserId, out var removed))
                    removed.Dispose();
            }
        };

        return pc;
    }

    private async void OnSdpOfferReceived(Guid serverId, Guid channelId, Guid fromUserId, string sdp)
    {
        if (!IsConnected || serverId != CurrentServerId || channelId != CurrentChannelId)
            return;

        try
        {
            var pc = CreatePeerConnection(fromUserId);
            _peers[fromUserId] = pc;

            var offer = new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp };
            pc.setRemoteDescription(offer);

            var answer = pc.createAnswer();
            await pc.setLocalDescription(answer);

            await _voiceHub.SendSdpAnswerAsync(serverId, channelId, fromUserId, answer.sdp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling SDP offer from {UserId}", fromUserId);
        }
    }

    private void OnSdpAnswerReceived(Guid serverId, Guid channelId, Guid fromUserId, string sdp)
    {
        if (!IsConnected || serverId != CurrentServerId || channelId != CurrentChannelId)
            return;

        if (_peers.TryGetValue(fromUserId, out var pc))
        {
            var answer = new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp };
            pc.setRemoteDescription(answer);
        }
    }

    private void OnIceCandidateReceived(Guid serverId, Guid channelId, Guid fromUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        if (!IsConnected || serverId != CurrentServerId || channelId != CurrentChannelId)
            return;

        if (_peers.TryGetValue(fromUserId, out var pc))
        {
            var iceCandidate = new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = sdpMid ?? "0",
                sdpMLineIndex = (ushort)(sdpMLineIndex ?? 0),
            };
            pc.addIceCandidate(iceCandidate);
        }
    }

    private void CleanupPeers()
    {
        foreach (var pc in _peers.Values)
        {
            pc.Dispose();
        }
        _peers.Clear();
    }

    public void Dispose()
    {
        _voiceHub.SdpOfferReceived -= OnSdpOfferReceived;
        _voiceHub.SdpAnswerReceived -= OnSdpAnswerReceived;
        _voiceHub.IceCandidateReceived -= OnIceCandidateReceived;
        CleanupPeers();
        CleanupAudio();
    }
}
