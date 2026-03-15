using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace Fennec.App.Services;

public record ActiveScreenSharer(Guid UserId, string Username, string? InstanceUrl);

public interface IVoiceCallService
{
    Task JoinAsync(Guid serverId, Guid channelId, string instanceUrl, Guid currentUserId);
    Task LeaveAsync();
    void SetMuted(bool muted);
    void SetDeafened(bool deafened);
    Task<List<CaptureTarget>> GetScreenShareTargetsAsync();
    Task StartScreenShareAsync(CaptureTarget target);
    Task StopScreenShareAsync();
    bool IsConnected { get; }
    Guid? CurrentServerId { get; }
    Guid? CurrentChannelId { get; }
    bool IsMuted { get; }
    bool IsDeafened { get; }
    bool IsScreenSharing { get; }
    IReadOnlyList<ActiveScreenSharer> ActiveScreenSharers { get; }
}

public class VoiceCallService : IVoiceCallService, IDisposable
{
    private readonly IVoiceHubService _voiceHub;
    private readonly IMessenger _messenger;
    private readonly ILogger<VoiceCallService> _logger;
    private readonly ISettingsStore _settingsStore;
    private readonly ISoundEffectService _soundEffects;
    private readonly IScreenCaptureService _screenCapture;
    private readonly ICursorPositionService _cursorPosition;

    private readonly Dictionary<Guid, RTCPeerConnection> _peers = new();
    private readonly Dictionary<Guid, RTCDataChannel> _cursorDataChannels = new();
    private readonly List<ActiveScreenSharer> _activeScreenSharers = [];
    private PortAudioEndPoint? _audioEndPoint;
    private ScreenShareVideoSource? _videoSource;
    private Guid _currentUserId;
    private bool _isSpeaking;
    private long _speakingLastActiveTicks;

    public bool IsConnected { get; private set; }
    public Guid? CurrentServerId { get; private set; }
    public Guid? CurrentChannelId { get; private set; }
    public string? CurrentInstanceUrl { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsDeafened { get; private set; }
    public bool IsScreenSharing { get; private set; }
    public IReadOnlyList<ActiveScreenSharer> ActiveScreenSharers => _activeScreenSharers;

    private static readonly RTCIceServer[] StunServers =
    [
        new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
    ];

    public VoiceCallService(IVoiceHubService voiceHub, IMessenger messenger, ILogger<VoiceCallService> logger, ISettingsStore settingsStore, ISoundEffectService soundEffects, IScreenCaptureService screenCapture, ICursorPositionService cursorPosition)
    {
        _voiceHub = voiceHub;
        _messenger = messenger;
        _logger = logger;
        _settingsStore = settingsStore;
        _soundEffects = soundEffects;
        _screenCapture = screenCapture;
        _cursorPosition = cursorPosition;

        _voiceHub.SdpOfferReceived += OnSdpOfferReceived;
        _voiceHub.SdpAnswerReceived += OnSdpAnswerReceived;
        _voiceHub.IceCandidateReceived += OnIceCandidateReceived;

        _messenger.Register<ScreenShareStartedMessage>(this, (_, msg) =>
        {
            if (!IsConnected || msg.ServerId != CurrentServerId) return;
            if (_activeScreenSharers.Any(s => s.UserId == msg.UserId)) return;
            _activeScreenSharers.Add(new ActiveScreenSharer(msg.UserId, msg.Username, msg.InstanceUrl));
        });
        _messenger.Register<ScreenShareStoppedMessage>(this, (_, msg) =>
        {
            if (!IsConnected || msg.ServerId != CurrentServerId) return;
            _activeScreenSharers.RemoveAll(s => s.UserId == msg.UserId);
        });
    }

    public async Task JoinAsync(Guid serverId, Guid channelId, string instanceUrl, Guid currentUserId)
    {
        if (IsConnected)
            await LeaveAsync();

        CurrentServerId = serverId;
        CurrentChannelId = channelId;
        CurrentInstanceUrl = instanceUrl;
        _currentUserId = currentUserId;

        await TryInitAudioEndPointAsync();

        var participants = await _voiceHub.JoinVoiceChannelAsync(serverId, channelId, instanceUrl);
        IsConnected = true;
        _ = _soundEffects.PlayAsync(SoundEffect.Join);
        _messenger.Send(new VoiceStateChangedMessage(true, serverId, channelId));

        // Notify UI about all current participants (including self)
        foreach (var participant in participants)
        {
            _messenger.Send(new VoiceParticipantJoinedMessage(serverId, channelId, participant.UserId, participant.Username, participant.InstanceUrl));
        }

        // Create peer connections to all existing participants except self (joiner offers)
        foreach (var participant in participants.Where(p => p.UserId != currentUserId))
        {
            await CreatePeerAndOffer(participant.UserId);
        }
    }

    public async Task LeaveAsync()
    {
        if (!IsConnected)
            return;

        if (IsScreenSharing)
            await StopScreenShareAsync();

        _ = _soundEffects.PlayAsync(SoundEffect.Leave);

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

        if (_isSpeaking)
        {
            _isSpeaking = false;
            _messenger.Send(new VoiceSpeakingChangedMessage(serverId, channelId, _currentUserId, false));
        }

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
        _ = _soundEffects.PlayAsync(muted ? SoundEffect.Mute : SoundEffect.Unmute);
        if (_audioEndPoint is not null)
        {
            if (muted)
                _ = _audioEndPoint.PauseAudio();
            else
                _ = _audioEndPoint.ResumeAudio();
        }

        // Muted → never speaking
        if (muted && _isSpeaking && IsConnected && CurrentServerId is not null && CurrentChannelId is not null)
        {
            _isSpeaking = false;
            _ = _voiceHub.SetSpeakingStateAsync(CurrentServerId.Value, CurrentChannelId.Value, false);
            _messenger.Send(new VoiceSpeakingChangedMessage(CurrentServerId.Value, CurrentChannelId.Value, _currentUserId, false));
        }

        if (IsConnected && CurrentServerId is not null && CurrentChannelId is not null)
            _ = _voiceHub.SetMuteStateAsync(CurrentServerId.Value, CurrentChannelId.Value, muted);
    }

    public void SetDeafened(bool deafened)
    {
        IsDeafened = deafened;
        _ = _soundEffects.PlayAsync(deafened ? SoundEffect.Deafen : SoundEffect.Undeafen);
        // When deafened, also mute
        if (deafened && !IsMuted)
            SetMuted(true);

        if (IsConnected && CurrentServerId is not null && CurrentChannelId is not null)
            _ = _voiceHub.SetDeafenStateAsync(CurrentServerId.Value, CurrentChannelId.Value, deafened);
    }

    public Task<List<CaptureTarget>> GetScreenShareTargetsAsync()
    {
        return _screenCapture.GetAvailableTargetsAsync();
    }

    public async Task StartScreenShareAsync(CaptureTarget target)
    {
        if (!IsConnected || IsScreenSharing || CurrentServerId is null || CurrentChannelId is null)
            return;

        _videoSource = new ScreenShareVideoSource(_logger);

        // Wire video source encoded samples to all peers
        _videoSource.OnVideoSourceEncodedSample += (durationRtpUnits, sample) =>
        {
            foreach (var (_, pc) in _peers)
            {
                pc.SendVideo(durationRtpUnits, sample);
            }
        };

        // Add VP8 video track to each existing peer and renegotiate
        foreach (var (remoteUserId, pc) in _peers)
        {
            await AddVideoTrackAndCursorChannel(remoteUserId, pc);

            // Renegotiate
            var offer = pc.createOffer();
            await pc.setLocalDescription(offer);
            await _voiceHub.SendSdpOfferAsync(CurrentServerId.Value, CurrentChannelId.Value, remoteUserId, offer.sdp);
        }

        // Start cursor tracking
        _cursorPosition.OnCursorChanged += OnCursorChanged;
        _cursorPosition.Start(target);

        // Start capture (also send frames locally for preview)
        await _screenCapture.StartAsync(target, (rgba, w, h) =>
        {
            _videoSource.OnFrame(rgba, w, h);
            _messenger.Send(new ScreenShareFrameMessage(_currentUserId, rgba, w, h));
        });

        IsScreenSharing = true;
        await _voiceHub.StartScreenShareAsync(CurrentServerId.Value, CurrentChannelId.Value);
        _logger.LogInformation("ScreenShare: Started sharing");
    }

    public async Task StopScreenShareAsync()
    {
        if (!IsScreenSharing)
            return;

        _cursorPosition.OnCursorChanged -= OnCursorChanged;
        _cursorPosition.Stop();

        await _screenCapture.StopAsync();

        // Close cursor data channels
        foreach (var dc in _cursorDataChannels.Values)
        {
            try { dc.close(); } catch { }
        }
        _cursorDataChannels.Clear();

        _videoSource?.Dispose();
        _videoSource = null;

        IsScreenSharing = false;

        if (IsConnected && CurrentServerId is not null && CurrentChannelId is not null)
            await _voiceHub.StopScreenShareAsync(CurrentServerId.Value, CurrentChannelId.Value);

        _logger.LogInformation("ScreenShare: Stopped sharing");
    }

    private void OnCursorChanged(float x, float y, Messages.CursorType type)
    {
        // Send cursor locally for preview
        _messenger.Send(new ScreenShareCursorMessage(_currentUserId, x, y, type));

        // Serialize cursor data: 2x float32 + 1 byte enum = 9 bytes
        var data = new byte[9];
        BitConverter.TryWriteBytes(data.AsSpan(0, 4), x);
        BitConverter.TryWriteBytes(data.AsSpan(4, 4), y);
        data[8] = (byte)type;

        foreach (var dc in _cursorDataChannels.Values)
        {
            try
            {
                if (dc.readyState == SIPSorcery.Net.RTCDataChannelState.open)
                    dc.send(data);
            }
            catch { }
        }
    }

    private async Task TryInitAudioEndPointAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            var inputIndex = PortAudioEndPoint.FindDeviceByName(settings.InputDeviceName, settings.AudioHostApi);
            var outputIndex = PortAudioEndPoint.FindDeviceByName(settings.OutputDeviceName, settings.AudioHostApi);

            _audioEndPoint = new PortAudioEndPoint(_logger, inputIndex, outputIndex);
            _audioEndPoint.OnCaptureLevel += OnCaptureLevel;
            _audioEndPoint.StartAudio();
            _audioEndPoint.StartAudioSink();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize PortAudio audio endpoint");
            _audioEndPoint = null;
        }
    }

    private void OnCaptureLevel(double rms)
    {
        if (!IsConnected || IsMuted || CurrentServerId is null || CurrentChannelId is null)
            return;

        var settings = _settingsStore.LoadAsync().GetAwaiter().GetResult();
        var threshold = settings.VoiceSensitivity;
        var now = Environment.TickCount64;

        if (rms > threshold)
        {
            _speakingLastActiveTicks = now;
            if (!_isSpeaking)
            {
                _isSpeaking = true;
                _ = _voiceHub.SetSpeakingStateAsync(CurrentServerId.Value, CurrentChannelId.Value, true);
                _messenger.Send(new VoiceSpeakingChangedMessage(CurrentServerId.Value, CurrentChannelId.Value, _currentUserId, true));
            }
        }
        else if (_isSpeaking && now - _speakingLastActiveTicks > 300)
        {
            _isSpeaking = false;
            _ = _voiceHub.SetSpeakingStateAsync(CurrentServerId.Value, CurrentChannelId.Value, false);
            _messenger.Send(new VoiceSpeakingChangedMessage(CurrentServerId.Value, CurrentChannelId.Value, _currentUserId, false));
        }
    }

    private void CleanupAudio()
    {
        if (_audioEndPoint is not null)
        {
            _audioEndPoint.OnCaptureLevel -= OnCaptureLevel;
            _audioEndPoint.Dispose();
            _audioEndPoint = null;
        }
    }

    private async Task AddVideoTrackAndCursorChannel(Guid remoteUserId, RTCPeerConnection pc)
    {
        var videoFormat = new SIPSorceryMedia.Abstractions.VideoFormat(
            SIPSorceryMedia.Abstractions.VideoCodecsEnum.VP8, 96);
        var videoTrack = new SIPSorcery.Net.MediaStreamTrack(videoFormat,
            SIPSorcery.Net.MediaStreamStatusEnum.SendOnly);
        pc.addTrack(videoTrack);

        try
        {
            var dc = await pc.createDataChannel("cursor");
            _cursorDataChannels[remoteUserId] = dc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ScreenShare: Could not create cursor data channel for {UserId}, cursor sharing disabled for this peer", remoteUserId);
        }
    }

    private async Task CreatePeerAndOffer(Guid remoteUserId)
    {
        var pc = CreatePeerConnection(remoteUserId);
        _peers[remoteUserId] = pc;

        if (IsScreenSharing)
            await AddVideoTrackAndCursorChannel(remoteUserId, pc);

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

        // Handle incoming audio and video
        pc.OnRtpPacketReceived += (ep, media, pkt) =>
        {
            if (media == SDPMediaTypesEnum.audio && !IsDeafened && _audioEndPoint is not null)
            {
                _audioEndPoint.GotAudioRtp(ep, pkt.Header.SyncSource, pkt.Header.SequenceNumber,
                    pkt.Header.Timestamp, pkt.Header.PayloadType, pkt.Header.MarkerBit == 1, pkt.Payload);
            }
            else if (media == SDPMediaTypesEnum.video)
            {
                HandleIncomingVideoRtp(remoteUserId, pkt);
            }
        };

        // Handle incoming data channels (cursor data from sharers)
        pc.ondatachannel += dc =>
        {
            if (dc.label == "cursor")
            {
                dc.onmessage += (_, _, data) =>
                {
                    if (data.Length == 9)
                    {
                        var x = BitConverter.ToSingle(data, 0);
                        var y = BitConverter.ToSingle(data, 4);
                        var cursorType = (Messages.CursorType)data[8];
                        _messenger.Send(new ScreenShareCursorMessage(remoteUserId, x, y, cursorType));
                    }
                };
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
            // Reuse existing peer connection for renegotiation (e.g. when remote adds video track).
            // Creating a new one would produce mismatched ICE credentials.
            bool reused;
            if (!_peers.TryGetValue(fromUserId, out var pc))
            {
                pc = CreatePeerConnection(fromUserId);
                _peers[fromUserId] = pc;
                reused = false;
            }
            else
            {
                reused = true;
            }

            var hasVideo = sdp.Contains("m=video");
            _logger.LogInformation("ScreenShare: SDP offer from {UserId}, reused={Reused}, hasVideo={HasVideo}, existingVideoTrack={HasTrack}, connState={State}",
                fromUserId, reused, hasVideo, pc.VideoLocalTrack != null, pc.connectionState);

            if (IsScreenSharing)
                await AddVideoTrackAndCursorChannel(fromUserId, pc);
            else if (hasVideo && pc.VideoLocalTrack == null)
            {
                var videoFormat = new SIPSorceryMedia.Abstractions.VideoFormat(VideoCodecsEnum.VP8, 96);
                var videoTrack = new MediaStreamTrack(videoFormat, MediaStreamStatusEnum.RecvOnly);
                pc.addTrack(videoTrack);
                _logger.LogInformation("ScreenShare: Added RecvOnly video track for {UserId}", fromUserId);
            }

            var offer = new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp };
            var setResult = pc.setRemoteDescription(offer);
            _logger.LogInformation("ScreenShare: setRemoteDescription result={Result} for {UserId}", setResult, fromUserId);

            var answer = pc.createAnswer();
            await pc.setLocalDescription(answer);

            _logger.LogInformation("ScreenShare: Sending SDP answer to {UserId}, answer has video={AnswerHasVideo}", fromUserId, answer.sdp?.Contains("m=video"));

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

    private readonly Dictionary<Guid, LibVpxDecoder> _videoDecoders = new();
    private int _videoRtpCount;

    private void HandleIncomingVideoRtp(Guid fromUserId, SIPSorcery.Net.RTPPacket pkt)
    {
        try
        {
            _videoRtpCount++;
            if (_videoRtpCount <= 3 || _videoRtpCount % 500 == 0)
                _logger.LogInformation("ScreenShare: Video RTP #{Count} from {UserId}, payloadLen={Len}", _videoRtpCount, fromUserId, pkt.Payload.Length);

            var payload = pkt.Payload;
            var skip = GetVp8DescriptorLength(payload);
            if (skip >= payload.Length) return;

            if (!_videoDecoders.TryGetValue(fromUserId, out var decoder))
                _videoDecoders[fromUserId] = decoder = new LibVpxDecoder();

            var vp8Data = payload.AsSpan(skip).ToArray();
            var result = decoder.Decode(vp8Data);
            if (result != null)
            {
                var (rgba, width, height) = result.Value;
                _logger.LogInformation("ScreenShare: Decoded frame {W}x{H} from {UserId}", width, height, fromUserId);
                _messenger.Send(new ScreenShareFrameMessage(fromUserId, rgba, width, height));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ScreenShare: Failed to decode video from {UserId}", fromUserId);
        }
    }

    /// <summary>
    /// Returns the byte length of the VP8 RTP payload descriptor (RFC 7741).
    /// The VP8 bitstream begins at this offset within pkt.Payload.
    /// </summary>
    private static int GetVp8DescriptorLength(byte[] payload)
    {
        if (payload.Length == 0) return 0;
        int i = 0;
        bool x = (payload[i] & 0x80) != 0;  // X — extension present
        i++;
        if (!x) return i;
        // Extension byte: I | L | T | K | RSV
        bool hasI = (payload[i] & 0x80) != 0;  // PictureID
        bool hasL = (payload[i] & 0x40) != 0;  // TL0PICIDX
        bool hasT = (payload[i] & 0x20) != 0;  // TID
        bool hasK = (payload[i] & 0x10) != 0;  // KEYIDX
        i++;
        if (hasI)
        {
            bool m = (payload[i] & 0x80) != 0;  // M — 15-bit PictureID
            i++;
            if (m) i++;
        }
        if (hasL) i++;
        if (hasT || hasK) i++;
        return i;
    }

    private void CleanupPeers()
    {
        foreach (var pc in _peers.Values)
            pc.Dispose();
        _peers.Clear();
        _cursorDataChannels.Clear();
        _activeScreenSharers.Clear();

        foreach (var dec in _videoDecoders.Values)
            dec.Dispose();
        _videoDecoders.Clear();
    }

    public void Dispose()
    {
        _voiceHub.SdpOfferReceived -= OnSdpOfferReceived;
        _voiceHub.SdpAnswerReceived -= OnSdpAnswerReceived;
        _voiceHub.IceCandidateReceived -= OnIceCandidateReceived;
        _cursorPosition.OnCursorChanged -= OnCursorChanged;
        _cursorPosition.Stop();
        _ = _screenCapture.StopAsync();
        _videoSource?.Dispose();
        CleanupPeers();
        CleanupAudio();
    }
}
