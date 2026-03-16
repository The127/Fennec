using Microsoft.AspNetCore.SignalR.Client;

namespace Fennec.Integration.Tests;

public class ScreenShareSignalingTests : IClassFixture<TestApiFactory>, IAsyncDisposable
{
    private readonly TestApiFactory _factory;

    private readonly Guid _userAId = Guid.NewGuid();
    private readonly Guid _userBId = Guid.NewGuid();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _channelId = Guid.NewGuid();

    private HubConnection? _hubA;
    private HubConnection? _hubB;

    public ScreenShareSignalingTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    private async Task SetupAsync()
    {
        Skip.IfNot(_factory.IsAvailable, "PostgreSQL not available on localhost:7891");

        var jwtA = HubTestHelper.CreateTestJwt(_userAId, "UserA", _factory.IssuerUrl);
        var jwtB = HubTestHelper.CreateTestJwt(_userBId, "UserB", _factory.IssuerUrl);

        _hubA = await HubTestHelper.ConnectToHubAsync(_factory, jwtA);
        _hubB = await HubTestHelper.ConnectToHubAsync(_factory, jwtB);

        // Both join the voice channel
        await _hubA.InvokeAsync("JoinVoiceChannel", _serverId, _channelId, _factory.IssuerUrl);
        await _hubB.InvokeAsync("JoinVoiceChannel", _serverId, _channelId, _factory.IssuerUrl);
    }

    [SkippableFact]
    public async Task SdpOffer_IsRelayedToTargetUser()
    {
        await SetupAsync();

        var receivedOffer = new TaskCompletionSource<(Guid ServerId, Guid ChannelId, Guid FromUserId, string Sdp)>();
        _hubB!.On<Guid, Guid, Guid, string>("ReceiveSdpOffer", (serverId, channelId, fromUserId, sdp) =>
            receivedOffer.TrySetResult((serverId, channelId, fromUserId, sdp)));

        var dummySdp = "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\n";
        await _hubA!.InvokeAsync("SendSdpOffer", _serverId, _channelId, _userBId, dummySdp);

        var result = await HubTestHelper.WaitForEventAsync(receivedOffer);
        Assert.Equal(_serverId, result.ServerId);
        Assert.Equal(_channelId, result.ChannelId);
        Assert.Equal(_userAId, result.FromUserId);
        Assert.Equal(dummySdp, result.Sdp);
    }

    [SkippableFact]
    public async Task SdpAnswer_IsRelayedToTargetUser()
    {
        await SetupAsync();

        var receivedAnswer = new TaskCompletionSource<(Guid ServerId, Guid ChannelId, Guid FromUserId, string Sdp)>();
        _hubA!.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer", (serverId, channelId, fromUserId, sdp) =>
            receivedAnswer.TrySetResult((serverId, channelId, fromUserId, sdp)));

        var dummySdp = "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\n";
        await _hubB!.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _userAId, dummySdp);

        var result = await HubTestHelper.WaitForEventAsync(receivedAnswer);
        Assert.Equal(_serverId, result.ServerId);
        Assert.Equal(_channelId, result.ChannelId);
        Assert.Equal(_userBId, result.FromUserId);
        Assert.Equal(dummySdp, result.Sdp);
    }

    [SkippableFact]
    public async Task IceCandidate_IsRelayedToTargetUser()
    {
        await SetupAsync();

        var receivedIce = new TaskCompletionSource<(Guid ServerId, Guid ChannelId, Guid FromUserId, string Candidate, string? SdpMid, int? SdpMLineIndex)>();
        _hubB!.On<Guid, Guid, Guid, string, string?, int?>("ReceiveIceCandidate",
            (serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex) =>
                receivedIce.TrySetResult((serverId, channelId, fromUserId, candidate, sdpMid, sdpMLineIndex)));

        var candidate = "candidate:1 1 udp 2130706431 192.168.1.1 50000 typ host";
        await _hubA!.InvokeAsync("SendIceCandidate", _serverId, _channelId, _userBId, candidate, "0", 0);

        var result = await HubTestHelper.WaitForEventAsync(receivedIce);
        Assert.Equal(_serverId, result.ServerId);
        Assert.Equal(_channelId, result.ChannelId);
        Assert.Equal(_userAId, result.FromUserId);
        Assert.Equal(candidate, result.Candidate);
        Assert.Equal("0", result.SdpMid);
        Assert.Equal(0, result.SdpMLineIndex);
    }

    [SkippableFact]
    public async Task ScreenShareStarted_IsBroadcastToOtherParticipants()
    {
        await SetupAsync();

        var receivedEvent = new TaskCompletionSource<(Guid ServerId, Guid ChannelId, Guid UserId, string Username, string? InstanceUrl)>();
        _hubB!.On<Guid, Guid, Guid, string, string?>("ScreenShareStarted",
            (serverId, channelId, userId, username, instanceUrl) =>
                receivedEvent.TrySetResult((serverId, channelId, userId, username, instanceUrl)));

        await _hubA!.InvokeAsync("StartScreenShare", _serverId, _channelId);

        var result = await HubTestHelper.WaitForEventAsync(receivedEvent);
        Assert.Equal(_serverId, result.ServerId);
        Assert.Equal(_channelId, result.ChannelId);
        Assert.Equal(_userAId, result.UserId);
        Assert.Equal("UserA", result.Username);
    }

    [SkippableFact]
    public async Task FullSignalingExchange_SdpOfferAnswerAndIceCandidates()
    {
        await SetupAsync();

        // Wire up all event handlers
        var screenShareStarted = new TaskCompletionSource<bool>();
        _hubB!.On<Guid, Guid, Guid, string, string?>("ScreenShareStarted",
            (_, _, _, _, _) => screenShareStarted.TrySetResult(true));

        var offerReceived = new TaskCompletionSource<string>();
        _hubB.On<Guid, Guid, Guid, string>("ReceiveSdpOffer",
            (_, _, _, sdp) => offerReceived.TrySetResult(sdp));

        var answerReceived = new TaskCompletionSource<string>();
        _hubA!.On<Guid, Guid, Guid, string>("ReceiveSdpAnswer",
            (_, _, _, sdp) => answerReceived.TrySetResult(sdp));

        var iceCandidateAToB = new TaskCompletionSource<string>();
        _hubB.On<Guid, Guid, Guid, string, string?, int?>("ReceiveIceCandidate",
            (_, _, _, candidate, _, _) => iceCandidateAToB.TrySetResult(candidate));

        var iceCandidateBToA = new TaskCompletionSource<string>();
        _hubA.On<Guid, Guid, Guid, string, string?, int?>("ReceiveIceCandidate",
            (_, _, _, candidate, _, _) => iceCandidateBToA.TrySetResult(candidate));

        // Step 1: UserA starts screen share
        await _hubA.InvokeAsync("StartScreenShare", _serverId, _channelId);
        Assert.True(await HubTestHelper.WaitForEventAsync(screenShareStarted));

        // Step 2: UserA sends SDP offer
        var offerSdp = "v=0\r\no=- offer\r\n";
        await _hubA.InvokeAsync("SendSdpOffer", _serverId, _channelId, _userBId, offerSdp);
        Assert.Equal(offerSdp, await HubTestHelper.WaitForEventAsync(offerReceived));

        // Step 3: UserB sends SDP answer
        var answerSdp = "v=0\r\no=- answer\r\n";
        await _hubB.InvokeAsync("SendSdpAnswer", _serverId, _channelId, _userAId, answerSdp);
        Assert.Equal(answerSdp, await HubTestHelper.WaitForEventAsync(answerReceived));

        // Step 4: Both exchange ICE candidates
        var candidateA = "candidate:1 1 udp 2130706431 10.0.0.1 50000 typ host";
        await _hubA.InvokeAsync("SendIceCandidate", _serverId, _channelId, _userBId, candidateA, "0", 0);
        Assert.Equal(candidateA, await HubTestHelper.WaitForEventAsync(iceCandidateAToB));

        var candidateB = "candidate:1 1 udp 2130706431 10.0.0.2 50001 typ host";
        await _hubB.InvokeAsync("SendIceCandidate", _serverId, _channelId, _userAId, candidateB, "0", 0);
        Assert.Equal(candidateB, await HubTestHelper.WaitForEventAsync(iceCandidateBToA));
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubA is not null)
        {
            try { await _hubA.InvokeAsync("LeaveVoiceChannel", _serverId, _channelId, _factory.IssuerUrl); } catch { }
            await _hubA.DisposeAsync();
        }
        if (_hubB is not null)
        {
            try { await _hubB.InvokeAsync("LeaveVoiceChannel", _serverId, _channelId, _factory.IssuerUrl); } catch { }
            await _hubB.DisposeAsync();
        }
    }
}
