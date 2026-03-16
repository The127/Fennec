using Microsoft.AspNetCore.SignalR.Client;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace Fennec.Integration.Tests;

/// <summary>
/// Tests that replicate the exact VoiceCallService SDP negotiation patterns
/// to catch cross-platform screen sharing bugs (SDP offer loops, ICE failures).
/// </summary>
public class ScreenShareWebRtcTests : IClassFixture<TestApiFactory>, IAsyncDisposable
{
    private readonly TestApiFactory _factory;

    private readonly Guid _sharerId = Guid.NewGuid();
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _channelId = Guid.NewGuid();

    private HubConnection? _sharerHub;
    private HubConnection? _viewerHub;
    private RTCPeerConnection? _sharerPc;
    private RTCPeerConnection? _viewerPc;

    public ScreenShareWebRtcTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Replicates VoiceCallService.CreatePeerConnection — audio SendRecv + ICE wiring through the hub.
    /// This is the base peer connection that both sharer and viewer start with.
    /// </summary>
    private RTCPeerConnection CreateBasePeerConnection()
    {
        var config = new RTCConfiguration
        {
            iceServers = [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }],
        };
        var pc = new RTCPeerConnection(config);

        // Audio track — matches VoiceCallService.CreatePeerConnection line 997-999
        var audioFormat = new AudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2);
        var audioTrack = new MediaStreamTrack(audioFormat, MediaStreamStatusEnum.SendRecv);
        pc.addTrack(audioTrack);

        return pc;
    }

    /// <summary>
    /// Replicates VoiceCallService.AddVideoTrackAndCursorChannel — adds SendOnly H264 video track.
    /// This is what the screen sharer adds to their peer connection.
    /// </summary>
    private static async Task AddSharerVideoTrack(RTCPeerConnection pc)
    {
        var videoFormat = new VideoFormat(VideoCodecsEnum.H264, 96);
        var videoTrack = new MediaStreamTrack(videoFormat, MediaStreamStatusEnum.SendOnly);
        pc.addTrack(videoTrack);

        // Cursor data channel — matches VoiceCallService line 842
        try { await pc.createDataChannel("cursor"); } catch { }
    }

    /// <summary>
    /// Adds RecvOnly H264 video track — what the viewer adds when receiving an offer with video.
    /// Matches VoiceCallService.OnSdpOfferReceived lines 1161-1167.
    /// </summary>
    private static void AddViewerVideoTrack(RTCPeerConnection pc)
    {
        var videoFormat = new VideoFormat(VideoCodecsEnum.H264, 96);
        var videoTrack = new MediaStreamTrack(videoFormat, MediaStreamStatusEnum.RecvOnly);
        pc.addTrack(videoTrack);
    }

    private void WireIceThroughHub(RTCPeerConnection pc, HubConnection hub, Guid targetUserId)
    {
        pc.onicecandidate += candidate =>
        {
            if (candidate != null)
            {
                _ = hub.InvokeAsync("SendIceCandidate", _serverId, _channelId, targetUserId,
                    candidate.candidate, candidate.sdpMid, (int?)candidate.sdpMLineIndex);
            }
        };
    }

    private void WireIceReceiver(HubConnection hub, RTCPeerConnection pc)
    {
        hub.On<Guid, Guid, Guid, string, string?, int?>("ReceiveIceCandidate",
            (_, _, _, candidateStr, sdpMid, sdpMLineIndex) =>
            {
                pc.addIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidateStr,
                    sdpMid = sdpMid ?? "0",
                    sdpMLineIndex = (ushort)(sdpMLineIndex ?? 0),
                });
            });
    }

    private async Task SetupHubsAndJoinVoice()
    {
        var jwtSharer = HubTestHelper.CreateTestJwt(_sharerId, "Sharer", _factory.IssuerUrl);
        var jwtViewer = HubTestHelper.CreateTestJwt(_viewerId, "Viewer", _factory.IssuerUrl);

        _sharerHub = await HubTestHelper.ConnectToHubAsync(_factory, jwtSharer);
        _viewerHub = await HubTestHelper.ConnectToHubAsync(_factory, jwtViewer);

        await _sharerHub.InvokeAsync("JoinVoiceChannel", _serverId, _channelId, _factory.IssuerUrl);
        await _viewerHub.InvokeAsync("JoinVoiceChannel", _serverId, _channelId, _factory.IssuerUrl);
    }

    /// <summary>
    /// Happy path: Screen sharer creates offer with audio+video(SendOnly),
    /// viewer responds with audio+video(RecvOnly). Both reach ICE connected.
    /// Replicates: VoiceCallService.CreatePeerAndOffer (sharer) → OnSdpOfferReceived (viewer).
    /// </summary>
    [SkippableFact]
    public async Task ScreenSharerOffersToViewer_IceConnects()
    {
        Skip.IfNot(_factory.IsAvailable, "PostgreSQL not available on localhost:7891");
        await SetupHubsAndJoinVoice();

        // Sharer creates peer with audio + video SendOnly (lines 960-967)
        _sharerPc = CreateBasePeerConnection();
        await AddSharerVideoTrack(_sharerPc);

        // Viewer creates peer with audio only (will add RecvOnly video on offer receipt)
        _viewerPc = CreateBasePeerConnection();

        var iceConnectedSharer = new TaskCompletionSource<bool>();
        var iceConnectedViewer = new TaskCompletionSource<bool>();

        _sharerPc.onconnectionstatechange += state =>
        {
            if (state == RTCPeerConnectionState.connected) iceConnectedSharer.TrySetResult(true);
            if (state == RTCPeerConnectionState.failed) iceConnectedSharer.TrySetResult(false);
        };
        _viewerPc.onconnectionstatechange += state =>
        {
            if (state == RTCPeerConnectionState.connected) iceConnectedViewer.TrySetResult(true);
            if (state == RTCPeerConnectionState.failed) iceConnectedViewer.TrySetResult(false);
        };

        // Wire ICE through hub
        WireIceThroughHub(_sharerPc, _sharerHub!, _viewerId);
        WireIceThroughHub(_viewerPc, _viewerHub!, _sharerId);
        WireIceReceiver(_viewerHub!, _viewerPc);
        WireIceReceiver(_sharerHub!, _sharerPc);

        // Viewer handles incoming SDP offer — replicates OnSdpOfferReceived lines 1136-1215
        _viewerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", async (_, _, _, sdp) =>
        {
            var hasVideo = sdp.Contains("m=video");

            // Line 1161-1167: viewer adds RecvOnly video track if offer has video
            if (hasVideo)
                AddViewerVideoTrack(_viewerPc);

            var setResult = _viewerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
            Assert.Equal(SetDescriptionResultEnum.OK, setResult);

            var answer = _viewerPc.createAnswer();
            await _viewerPc.setLocalDescription(answer);
            await _viewerHub.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _sharerId, answer.sdp);
        });

        // Sharer handles incoming SDP answer — replicates OnSdpAnswerReceived lines 1217-1239
        _sharerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (_, _, _, sdp) =>
        {
            var result = _sharerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
            Assert.Equal(SetDescriptionResultEnum.OK, result);
        });

        // Sharer creates offer and sends (line 979-984)
        var offer = _sharerPc.createOffer();
        await _sharerPc.setLocalDescription(offer);

        // Verify offer includes video m-line
        Assert.Contains("m=video", offer.sdp);

        await _sharerHub.InvokeAsync("SendSdpOffer", _serverId, _channelId, _viewerId, offer.sdp);

        var timeout = TimeSpan.FromSeconds(15);
        Assert.True(await HubTestHelper.WaitForEventAsync(iceConnectedSharer, timeout), "Sharer failed to reach ICE connected");
        Assert.True(await HubTestHelper.WaitForEventAsync(iceConnectedViewer, timeout), "Viewer failed to reach ICE connected");
    }

    /// <summary>
    /// Counter-offer scenario: Viewer joins first with audio-only offer.
    /// Screen sharer answers, then sends counter-offer with video.
    /// Replicates the counter-offer path: OnSdpOfferReceived lines 1204-1209.
    /// This is the most likely failure mode for the 20s SDP loop bug.
    /// </summary>
    [SkippableFact]
    public async Task ViewerOffersAudioOnly_SharerCounterOffersWithVideo_IceConnects()
    {
        Skip.IfNot(_factory.IsAvailable, "PostgreSQL not available on localhost:7891");
        await SetupHubsAndJoinVoice();

        _sharerPc = CreateBasePeerConnection();
        _viewerPc = CreateBasePeerConnection();

        var iceConnectedSharer = new TaskCompletionSource<bool>();
        var iceConnectedViewer = new TaskCompletionSource<bool>();
        var offerCount = 0;
        var counterOfferReceived = new TaskCompletionSource<bool>();

        _sharerPc.onconnectionstatechange += state =>
        {
            if (state == RTCPeerConnectionState.connected) iceConnectedSharer.TrySetResult(true);
            if (state == RTCPeerConnectionState.failed) iceConnectedSharer.TrySetResult(false);
        };
        _viewerPc.onconnectionstatechange += state =>
        {
            if (state == RTCPeerConnectionState.connected) iceConnectedViewer.TrySetResult(true);
            if (state == RTCPeerConnectionState.failed) iceConnectedViewer.TrySetResult(false);
        };

        WireIceThroughHub(_sharerPc, _sharerHub!, _viewerId);
        WireIceThroughHub(_viewerPc, _viewerHub!, _sharerId);
        WireIceReceiver(_viewerHub!, _viewerPc);
        WireIceReceiver(_sharerHub!, _sharerPc);

        // Sharer handles incoming SDP offer — replicates OnSdpOfferReceived with IsScreenSharing=true
        _sharerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", async (_, _, _, sdp) =>
        {
            Interlocked.Increment(ref offerCount);

            var hasVideo = sdp.Contains("m=video");

            // Line 1159-1160: sharer always adds SendOnly video track
            await AddSharerVideoTrack(_sharerPc);

            var setResult = _sharerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
            if (setResult != SetDescriptionResultEnum.OK)
                return; // In real code this triggers RebuildPeerForReceiveAsync

            var answer = _sharerPc.createAnswer();
            await _sharerPc.setLocalDescription(answer);
            await _sharerHub.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _viewerId, answer.sdp);

            // Line 1204-1209: counter-offer if incoming offer had no video
            if (!hasVideo)
            {
                var counterOffer = _sharerPc.createOffer();
                await _sharerPc.setLocalDescription(counterOffer);
                await _sharerHub.InvokeAsync("SendSdpOffer", _serverId, _channelId, _viewerId, counterOffer.sdp);
            }
        });

        // Viewer handles incoming SDP offer (the counter-offer from sharer)
        _viewerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", async (_, _, _, sdp) =>
        {
            var hasVideo = sdp.Contains("m=video");

            // Line 1161-1167: add RecvOnly video if offer has video
            if (hasVideo)
                AddViewerVideoTrack(_viewerPc);

            var setResult = _viewerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
            if (setResult != SetDescriptionResultEnum.OK)
                return;

            var answer = _viewerPc.createAnswer();
            await _viewerPc.setLocalDescription(answer);
            await _viewerHub.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _sharerId, answer.sdp);
            counterOfferReceived.TrySetResult(true);
        });

        // Sharer + viewer handle SDP answers
        _sharerHub.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (_, _, _, sdp) =>
        {
            _sharerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
        });
        _viewerHub.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (_, _, _, sdp) =>
        {
            _viewerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
        });

        // Viewer sends audio-only offer (no video — viewer is NOT screen sharing)
        var offer = _viewerPc.createOffer();
        await _viewerPc.setLocalDescription(offer);
        Assert.DoesNotContain("m=video", offer.sdp); // Viewer has no video track
        await _viewerHub.InvokeAsync("SendSdpOffer", _serverId, _channelId, _sharerId, offer.sdp);

        // Wait for counter-offer to be received and processed
        Assert.True(await HubTestHelper.WaitForEventAsync(counterOfferReceived, TimeSpan.FromSeconds(10)),
            "Viewer never received counter-offer with video from sharer");

        var timeout = TimeSpan.FromSeconds(15);
        Assert.True(await HubTestHelper.WaitForEventAsync(iceConnectedSharer, timeout), "Sharer failed to reach ICE connected");
        Assert.True(await HubTestHelper.WaitForEventAsync(iceConnectedViewer, timeout), "Viewer failed to reach ICE connected");

        // Verify no infinite offer loop — should be exactly 1 offer from viewer + counter-offer handling
        await Task.Delay(2000); // Wait to see if more offers come in
        Assert.True(offerCount <= 2, $"Offer loop detected: sharer received {offerCount} offers (expected <= 2)");
    }

    /// <summary>
    /// Tests renegotiation: both connect with audio only, then sharer starts screen share
    /// and renegotiates by sending a new offer with video.
    /// Replicates: VoiceCallService.RenegotiateVideoTrack (line 851-858).
    /// </summary>
    [SkippableFact]
    public async Task AudioOnlyThenRenegotiateWithVideo_IceStaysConnected()
    {
        Skip.IfNot(_factory.IsAvailable, "PostgreSQL not available on localhost:7891");
        await SetupHubsAndJoinVoice();

        _sharerPc = CreateBasePeerConnection();
        _viewerPc = CreateBasePeerConnection();

        var iceConnectedSharer = new TaskCompletionSource<bool>();
        var iceConnectedViewer = new TaskCompletionSource<bool>();
        var renegotiationComplete = new TaskCompletionSource<bool>();

        _sharerPc.onconnectionstatechange += state =>
        {
            if (state == RTCPeerConnectionState.connected) iceConnectedSharer.TrySetResult(true);
            if (state == RTCPeerConnectionState.failed) iceConnectedSharer.TrySetResult(false);
        };
        _viewerPc.onconnectionstatechange += state =>
        {
            if (state == RTCPeerConnectionState.connected) iceConnectedViewer.TrySetResult(true);
            if (state == RTCPeerConnectionState.failed) iceConnectedViewer.TrySetResult(false);
        };

        WireIceThroughHub(_sharerPc, _sharerHub!, _viewerId);
        WireIceThroughHub(_viewerPc, _viewerHub!, _sharerId);
        WireIceReceiver(_viewerHub!, _viewerPc);
        WireIceReceiver(_sharerHub!, _sharerPc);

        // Viewer handles all incoming offers (initial audio-only + renegotiation with video)
        _viewerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", async (_, _, _, sdp) =>
        {
            var hasVideo = sdp.Contains("m=video");
            if (hasVideo)
                AddViewerVideoTrack(_viewerPc);

            var setResult = _viewerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
            if (setResult != SetDescriptionResultEnum.OK)
                return;

            var answer = _viewerPc.createAnswer();
            await _viewerPc.setLocalDescription(answer);
            await _viewerHub.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _sharerId, answer.sdp);

            if (hasVideo)
                renegotiationComplete.TrySetResult(true);
        });

        _sharerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (_, _, _, sdp) =>
        {
            _sharerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
        });

        // Phase 1: audio-only connection
        var offer = _sharerPc.createOffer();
        await _sharerPc.setLocalDescription(offer);
        Assert.DoesNotContain("m=video", offer.sdp);
        await _sharerHub.InvokeAsync("SendSdpOffer", _serverId, _channelId, _viewerId, offer.sdp);

        var timeout = TimeSpan.FromSeconds(15);
        Assert.True(await HubTestHelper.WaitForEventAsync(iceConnectedSharer, timeout), "Initial connection failed");

        // Phase 2: sharer starts screen share → renegotiate with video
        // Replicates RenegotiateVideoTrack (line 851-858)
        await AddSharerVideoTrack(_sharerPc);
        var renegOffer = _sharerPc.createOffer();
        await _sharerPc.setLocalDescription(renegOffer);
        Assert.Contains("m=video", renegOffer.sdp);
        await _sharerHub.InvokeAsync("SendSdpOffer", _serverId, _channelId, _viewerId, renegOffer.sdp);

        Assert.True(await HubTestHelper.WaitForEventAsync(renegotiationComplete, timeout),
            "Renegotiation with video never completed");

        // Verify both sides still connected after renegotiation
        Assert.True(await HubTestHelper.WaitForEventAsync(iceConnectedViewer, timeout),
            "Viewer lost connection after renegotiation");
    }

    /// <summary>
    /// Tests that the counter-offer path doesn't create an infinite SDP offer loop.
    /// This is the suspected cause of the "SDP offers every 20s" bug.
    /// Simulates: Viewer audio-only offer → Sharer answers + counter-offers →
    /// Viewer answers counter-offer → exchange should STOP.
    /// </summary>
    [SkippableFact]
    public async Task CounterOffer_DoesNotCauseInfiniteLoop()
    {
        Skip.IfNot(_factory.IsAvailable, "PostgreSQL not available on localhost:7891");
        await SetupHubsAndJoinVoice();

        _sharerPc = CreateBasePeerConnection();
        _viewerPc = CreateBasePeerConnection();

        var sharerOfferCount = 0;
        var viewerOfferCount = 0;

        WireIceThroughHub(_sharerPc, _sharerHub!, _viewerId);
        WireIceThroughHub(_viewerPc, _viewerHub!, _sharerId);
        WireIceReceiver(_viewerHub!, _viewerPc);
        WireIceReceiver(_sharerHub!, _sharerPc);

        // Sharer: receives offers, answers, counter-offers if no video (IsScreenSharing=true path)
        _sharerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", async (_, _, _, sdp) =>
        {
            Interlocked.Increment(ref sharerOfferCount);
            var hasVideo = sdp.Contains("m=video");

            // Only add video track once (real code tracks this with AddVideoTrackAndCursorChannel)
            if (_sharerPc.VideoLocalTrack == null)
                await AddSharerVideoTrack(_sharerPc);

            var setResult = _sharerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
            if (setResult != SetDescriptionResultEnum.OK) return;

            var answer = _sharerPc.createAnswer();
            await _sharerPc.setLocalDescription(answer);
            await _sharerHub.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _viewerId, answer.sdp);

            // Counter-offer only if no video in the incoming offer
            if (!hasVideo)
            {
                var counterOffer = _sharerPc.createOffer();
                await _sharerPc.setLocalDescription(counterOffer);
                await _sharerHub.InvokeAsync("SendSdpOffer", _serverId, _channelId, _viewerId, counterOffer.sdp);
            }
        });

        // Viewer: receives offers (counter-offers from sharer), answers with RecvOnly video
        // NOTE: viewer does NOT send counter-offers (IsScreenSharing=false)
        _viewerHub!.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", async (_, _, _, sdp) =>
        {
            Interlocked.Increment(ref viewerOfferCount);
            var hasVideo = sdp.Contains("m=video");

            if (hasVideo && _viewerPc.VideoLocalTrack == null)
                AddViewerVideoTrack(_viewerPc);

            var setResult = _viewerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
            if (setResult != SetDescriptionResultEnum.OK) return;

            var answer = _viewerPc.createAnswer();
            await _viewerPc.setLocalDescription(answer);
            await _viewerHub.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _sharerId, answer.sdp);
            // Viewer does NOT counter-offer (not screen sharing)
        });

        // Both handle answers
        _sharerHub.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (_, _, _, sdp) =>
            _sharerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp }));
        _viewerHub.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (_, _, _, sdp) =>
            _viewerPc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp }));

        // Viewer sends audio-only offer (the trigger for the potential loop)
        var offer = _viewerPc.createOffer();
        await _viewerPc.setLocalDescription(offer);
        await _viewerHub.InvokeAsync("SendSdpOffer", _serverId, _channelId, _sharerId, offer.sdp);

        // Wait for the exchange to settle
        await Task.Delay(5000);

        // The exchange should converge:
        // 1. Viewer sends audio-only offer → sharerOfferCount=1
        // 2. Sharer answers + sends counter-offer with video → viewerOfferCount=1
        // 3. Viewer answers counter-offer with video → sharerOfferCount stays 1 (no counter-offer because it HAS video now)
        // Total: sharerOfferCount=1, viewerOfferCount=1
        Assert.True(sharerOfferCount <= 2,
            $"SDP offer loop detected: sharer received {sharerOfferCount} offers (expected <= 2)");
        Assert.True(viewerOfferCount <= 2,
            $"SDP offer loop detected: viewer received {viewerOfferCount} offers (expected <= 2)");
    }

    public async ValueTask DisposeAsync()
    {
        _sharerPc?.close();
        _viewerPc?.close();
        _sharerPc?.Dispose();
        _viewerPc?.Dispose();

        if (_sharerHub is not null)
        {
            try { await _sharerHub.InvokeAsync("LeaveVoiceChannel", _serverId, _channelId, _factory.IssuerUrl); } catch { }
            await _sharerHub.DisposeAsync();
        }
        if (_viewerHub is not null)
        {
            try { await _viewerHub.InvokeAsync("LeaveVoiceChannel", _serverId, _channelId, _factory.IssuerUrl); } catch { }
            await _viewerHub.DisposeAsync();
        }
    }
}
