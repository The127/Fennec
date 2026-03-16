using System.Diagnostics;
using System.Net;
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
    Task StartScreenShareAsync(CaptureTarget target, string resolution, int bitrateKbps, int frameRate);
    Task UpdateScreenShareSettingsAsync(string resolution, int bitrateKbps, int frameRate);
    Task StopScreenShareAsync();
    bool IsConnected { get; }
    Guid? CurrentServerId { get; }
    Guid? CurrentChannelId { get; }
    bool IsMuted { get; }
    bool IsDeafened { get; }
    bool IsScreenSharing { get; }
    IReadOnlyList<ActiveScreenSharer> ActiveScreenSharers { get; }
    ScreenShareMetrics? GetMetrics(Guid userId);
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
    private readonly Dictionary<Guid, ScreenShareMetrics> _screenShareMetrics = new();
    private PortAudioEndPoint? _audioEndPoint;
    private ScreenShareVideoSource? _videoSource;
    private Guid _currentUserId;
    private bool _isSpeaking;
    private long _speakingLastActiveTicks;
    private bool _isLeaving;

    // Sender FPS tracking
    private int _senderFrameCount;
    private long _senderFpsTimestamp;

    // Receiver FPS tracking per user
    private readonly Dictionary<Guid, int> _receiverFrameCounts = new();
    private readonly Dictionary<Guid, long> _receiverFpsTimestamps = new();

    public bool IsConnected { get; private set; }
    public Guid? CurrentServerId { get; private set; }
    public Guid? CurrentChannelId { get; private set; }
    public string? CurrentInstanceUrl { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsDeafened { get; private set; }
    public bool IsScreenSharing { get; private set; }
    public IReadOnlyList<ActiveScreenSharer> ActiveScreenSharers => _activeScreenSharers;

    public ScreenShareMetrics? GetMetrics(Guid userId)
    {
        _screenShareMetrics.TryGetValue(userId, out var m);
        return m;
    }

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

        _isLeaving = true;

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
        _isLeaving = false;

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

    public async Task StartScreenShareAsync(CaptureTarget target, string resolution, int bitrateKbps, int frameRate)
    {
        if (!IsConnected || IsScreenSharing || CurrentServerId is null || CurrentChannelId is null)
            return;

        var (maxW, maxH) = ScreenShareVideoSource.ResolutionPresetToDimensions(resolution);
        // "Native" returns (0,0) — use the actual target dimensions
        if (maxW == 0 || maxH == 0)
        {
            maxW = target.Width > 0 ? target.Width : 1920;
            maxH = target.Height > 0 ? target.Height : 1080;
        }
        _videoSource = new ScreenShareVideoSource(_logger, maxW, maxH, bitrateKbps, (uint)frameRate);

        var senderMetrics = new ScreenShareMetrics { IsSender = true };
        _screenShareMetrics[_currentUserId] = senderMetrics;
        _senderFrameCount = 0;
        _senderFpsTimestamp = Stopwatch.GetTimestamp();

        // Wire video source encoded samples to all peers (H.264 access units in Annex B format)
        _videoSource.OnVideoSourceEncodedSample += (durationRtpUnits, sample) =>
        {
            senderMetrics.EncodedSizeKb.Add(sample.Length / 1024.0);
            foreach (var (_, pc) in _peers)
            {
                pc.VideoStream?.SendH264Frame(durationRtpUnits, 96, sample);
            }
        };

        // Add H.264 video track to each existing peer and renegotiate
        _logger.LogInformation("ScreenShare: Renegotiating with {Count} peers", _peers.Count);
        foreach (var (remoteUserId, pc) in _peers)
        {
            try
            {
                _logger.LogInformation("ScreenShare: Adding video track for peer {UserId}, connState={State}", remoteUserId, pc.connectionState);
                await AddVideoTrackAndCursorChannel(remoteUserId, pc);

                // Renegotiate
                var offer = pc.createOffer();
                var hasVideo = offer.sdp?.Contains("m=video") ?? false;
                _logger.LogInformation("ScreenShare: Created renegotiation offer for {UserId}, hasVideo={HasVideo}", remoteUserId, hasVideo);
                await pc.setLocalDescription(offer);
                await _voiceHub.SendSdpOfferAsync(CurrentServerId.Value, CurrentChannelId.Value, remoteUserId, offer.sdp);
                _logger.LogInformation("ScreenShare: Sent renegotiation offer to {UserId}", remoteUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScreenShare: Failed to renegotiate with {UserId}", remoteUserId);
            }
        }

        // Start cursor tracking
        _cursorPosition.OnCursorChanged += OnCursorChanged;
        _cursorPosition.Start(target);

        // Preview callback — local display only, no encoding
        void OnPreviewFrame(byte[] rgba, int w, int h)
        {
            senderMetrics.CaptureWidth = w;
            senderMetrics.CaptureHeight = h;

            // Track capture FPS
            _senderFrameCount++;
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(_senderFpsTimestamp);
            if (elapsed.TotalSeconds >= 1.0)
            {
                senderMetrics.CaptureFps.Add(_senderFrameCount / elapsed.TotalSeconds);
                _senderFrameCount = 0;
                _senderFpsTimestamp = now;
            }

            _messenger.Send(new ScreenShareFrameMessage(_currentUserId, rgba, w, h, Stopwatch.GetTimestamp()));
        }

        if (_screenCapture is ScreenCapture.MacOsScreenCaptureService macCapture)
        {
            // Fused mode: native capture does hardware H.264 encoding via VideoToolbox
            // NAL units go to peers, RGBA preview goes to local display
            await macCapture.StartFusedAsync(target, maxW, maxH, bitrateKbps,
                frameRate,
                onNal: (nal, pts, isKf) => _videoSource!.OnNalUnit(nal, pts, isKf),
                onPreview: OnPreviewFrame);

            if (!macCapture.IsCapturing)
            {
                _logger.LogError("ScreenShare: Fused capture failed to start, aborting");
                _cursorPosition.OnCursorChanged -= OnCursorChanged;
                _cursorPosition.Stop();
                _videoSource?.Dispose();
                _videoSource = null;
                return;
            }
        }
        else
        {
            // Standalone mode (Linux): software H.264 encoding
            await _screenCapture.StartAsync(target, (rgba, w, h) =>
            {
                var encodeSw = Stopwatch.StartNew();
                _videoSource!.OnFrame(rgba, w, h);
                encodeSw.Stop();
                senderMetrics.EncodeTimeMs.Add(encodeSw.Elapsed.TotalMilliseconds);

                OnPreviewFrame(rgba, w, h);
            });
        }

        IsScreenSharing = true;
        await _voiceHub.StartScreenShareAsync(CurrentServerId.Value, CurrentChannelId.Value);
        _logger.LogInformation("ScreenShare: Started sharing");
    }

    public Task UpdateScreenShareSettingsAsync(string resolution, int bitrateKbps, int frameRate)
    {
        if (!IsScreenSharing || _videoSource is null)
            return Task.CompletedTask;

        if (_screenCapture is ScreenCapture.MacOsScreenCaptureService macCapture)
        {
            // Fused mode: update via capture service
            macCapture.UpdateFusedBitrate(bitrateKbps);
            macCapture.UpdateFusedFps(frameRate);
        }
        else
        {
            // Standalone mode: update via video source
            _videoSource.UpdateBitrate(bitrateKbps);
            _videoSource.UpdateFps(frameRate);
            _videoSource.UpdateResolution(resolution);
        }

        _logger.LogInformation("ScreenShare: Settings updated — resolution={Resolution}, bitrate={Bitrate}Kbps, fps={Fps}",
            resolution, bitrateKbps, frameRate);
        return Task.CompletedTask;
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
            SIPSorceryMedia.Abstractions.VideoCodecsEnum.H264, 96);
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
                // Guard: only send if this peer is still the active one for this user
                if (!IsMuted && _peers.TryGetValue(remoteUserId, out var activePc) && activePc == pc)
                    pc.SendAudio(durationRtpUnits, sample);
            };
        }

        // Handle incoming audio (raw RTP for PortAudio)
        pc.OnRtpPacketReceived += (ep, media, pkt) =>
        {
            if (!_peers.TryGetValue(remoteUserId, out var activePc) || activePc != pc)
                return;

            if (media == SDPMediaTypesEnum.audio && !IsDeafened && _audioEndPoint is not null)
            {
                _audioEndPoint.GotAudioRtp(ep, pkt.Header.SyncSource, pkt.Header.SequenceNumber,
                    pkt.Header.Timestamp, pkt.Header.PayloadType, pkt.Header.MarkerBit == 1, pkt.Payload);
            }
        };

        // Handle incoming H.264 video frames (depacketised by SIPSorcery)
        pc.OnVideoFrameReceived += (IPEndPoint ep, uint timestamp, byte[] frame, VideoFormat format) =>
        {
            if (!_peers.TryGetValue(remoteUserId, out var activePc) || activePc != pc)
                return;

            HandleIncomingVideoFrame(remoteUserId, frame);
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
            if (state is RTCPeerConnectionState.failed or RTCPeerConnectionState.closed)
            {
                if (_peers.Remove(remoteUserId, out var removed))
                    removed.Dispose();

                // Auto-reconnect if the closure was unexpected (not during LeaveAsync)
                if (!_isLeaving && IsConnected && CurrentServerId is not null && CurrentChannelId is not null)
                {
                    _logger.LogInformation("Peer {RemoteUser} closed unexpectedly, re-establishing connection", remoteUserId);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(1000); // Brief delay before reconnect
                            if (!IsConnected || _isLeaving || _peers.ContainsKey(remoteUserId))
                                return;
                            await CreatePeerAndOffer(remoteUserId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to re-establish peer connection to {RemoteUser}", remoteUserId);
                        }
                    });
                }
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
                var videoFormat = new SIPSorceryMedia.Abstractions.VideoFormat(VideoCodecsEnum.H264, 96);
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

            // If we're screen sharing but the peer's offer didn't include video,
            // send a counter-offer so the remote side can receive our video track
            if (IsScreenSharing && !hasVideo)
            {
                _logger.LogInformation("ScreenShare: Sending counter-offer with video to {UserId}", fromUserId);
                var counterOffer = pc.createOffer();
                await pc.setLocalDescription(counterOffer);
                await _voiceHub.SendSdpOfferAsync(serverId, channelId, fromUserId, counterOffer.sdp);
            }
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

    private readonly Dictionary<Guid, H264Decoder> _videoDecoders = new();
    private int _videoFrameCount;
    private int? _cachedViewerDownscalePercent;

    private void HandleIncomingVideoFrame(Guid fromUserId, byte[] accessUnit)
    {
        try
        {
            if (!_videoDecoders.TryGetValue(fromUserId, out var decoder))
                _videoDecoders[fromUserId] = decoder = new H264Decoder(_logger);

            if (!_screenShareMetrics.TryGetValue(fromUserId, out var metrics))
                _screenShareMetrics[fromUserId] = metrics = new ScreenShareMetrics { IsSender = false };

            // Parse Annex B NAL units from the access unit
            var nals = ParseAnnexBNals(accessUnit);
            foreach (var nal in nals)
            {
                var decodeSw = Stopwatch.StartNew();
                decoder.Decode(nal, (rgba, width, height) =>
                {
                    decodeSw.Stop();
                    metrics.DecodeTimeMs.Add(decodeSw.Elapsed.TotalMilliseconds);

                    _videoFrameCount++;
                    if (_videoFrameCount <= 3 || _videoFrameCount % 100 == 0)
                        _logger.LogInformation("ScreenShare: Decoded H.264 frame #{Num} {W}x{H} from {UserId}",
                            _videoFrameCount, width, height, fromUserId);

                    // Track receive FPS
                    if (!_receiverFrameCounts.ContainsKey(fromUserId))
                    {
                        _receiverFrameCounts[fromUserId] = 0;
                        _receiverFpsTimestamps[fromUserId] = Stopwatch.GetTimestamp();
                    }
                    _receiverFrameCounts[fromUserId]++;
                    var elapsed = Stopwatch.GetElapsedTime(_receiverFpsTimestamps[fromUserId]);
                    if (elapsed.TotalSeconds >= 1.0)
                    {
                        metrics.ReceiveFps.Add(_receiverFrameCounts[fromUserId] / elapsed.TotalSeconds);
                        _receiverFrameCounts[fromUserId] = 0;
                        _receiverFpsTimestamps[fromUserId] = Stopwatch.GetTimestamp();
                    }

                    // Apply viewer downscale if configured
                    _cachedViewerDownscalePercent ??= _settingsStore.LoadAsync().GetAwaiter().GetResult().ViewerDownscalePercent;
                    var scale = _cachedViewerDownscalePercent.Value;
                    if (scale < 100 && scale > 0)
                    {
                        var dstW = width * scale / 100 & ~1;
                        var dstH = height * scale / 100 & ~1;
                        if (dstW > 0 && dstH > 0)
                        {
                            var scaleSw = Stopwatch.StartNew();
                            rgba = ScreenShareVideoSource.BilinearDownscaleRgba(rgba, width, height, dstW, dstH);
                            scaleSw.Stop();
                            metrics.DownscaleTimeMs.Add(scaleSw.Elapsed.TotalMilliseconds);
                            width = dstW;
                            height = dstH;
                        }
                    }

                    _messenger.Send(new ScreenShareFrameMessage(fromUserId, rgba, width, height, Stopwatch.GetTimestamp()));
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ScreenShare: Failed to decode video from {UserId}", fromUserId);
        }
    }

    /// <summary>
    /// Parses Annex B formatted data into individual NAL units (without start codes).
    /// </summary>
    private static List<byte[]> ParseAnnexBNals(byte[] data)
    {
        var nals = new List<byte[]>();
        int i = 0;

        while (i < data.Length)
        {
            // Find start code
            int scLen = 0;
            if (i + 3 <= data.Length && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 1)
                scLen = 3;
            else if (i + 4 <= data.Length && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 0 && data[i + 3] == 1)
                scLen = 4;
            else { i++; continue; }

            int nalStart = i + scLen;

            // Find next start code
            int nalEnd = data.Length;
            for (int j = nalStart + 1; j < data.Length - 2; j++)
            {
                if (data[j] == 0 && data[j + 1] == 0 &&
                    (data[j + 2] == 1 || (j + 3 < data.Length && data[j + 2] == 0 && data[j + 3] == 1)))
                {
                    nalEnd = j;
                    break;
                }
            }

            if (nalEnd > nalStart)
            {
                var nal = new byte[nalEnd - nalStart];
                Buffer.BlockCopy(data, nalStart, nal, 0, nal.Length);
                nals.Add(nal);
            }

            i = nalEnd;
        }

        return nals;
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
