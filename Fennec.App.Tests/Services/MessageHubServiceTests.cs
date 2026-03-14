using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Services;
using Fennec.Client;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Fennec.App.Tests.Services;

public class MessageHubServiceTests
{
    private readonly IMessageHubClient _hubClient = Substitute.For<IMessageHubClient>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly ILogger<MessageHubService> _logger = Substitute.For<ILogger<MessageHubService>>();

    private MessageHubService CreateService() => new(_hubClient, _messenger, _logger);

    [Fact]
    public async Task Reconnect_resubscribes_to_server_groups()
    {
        var service = CreateService();
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();

        // Connect and subscribe
        await service.ConnectAsync("https://test.instance", "token");
        await service.SubscribeToServerAsync(serverId1);
        await service.SubscribeToServerAsync(serverId2);

        // Clear received calls
        _hubClient.ClearReceivedCalls();

        // Simulate reconnect by raising ConnectionStateChanged with Connected
        _hubClient.ConnectionStateChanged += Raise.Event<Action<HubConnectionStatus>>(HubConnectionStatus.Connected);

        // Verify re-subscription
        await _hubClient.Received().SubscribeToServerAsync(serverId1);
        await _hubClient.Received().SubscribeToServerAsync(serverId2);
    }

    [Fact]
    public async Task Reconnect_resubscribes_to_channel_if_one_was_active()
    {
        var service = CreateService();
        var serverId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        await service.ConnectAsync("https://test.instance", "token");
        await service.SubscribeToChannelAsync(serverId, channelId);

        _hubClient.ClearReceivedCalls();

        _hubClient.ConnectionStateChanged += Raise.Event<Action<HubConnectionStatus>>(HubConnectionStatus.Connected);

        await _hubClient.Received().SubscribeToChannelAsync(serverId, channelId);
    }

    [Fact]
    public async Task UnsubscribeFromServer_removes_from_reconnect_set()
    {
        var service = CreateService();
        var serverId = Guid.NewGuid();

        await service.ConnectAsync("https://test.instance", "token");
        await service.SubscribeToServerAsync(serverId);
        await service.UnsubscribeFromServerAsync(serverId);

        _hubClient.ClearReceivedCalls();

        _hubClient.ConnectionStateChanged += Raise.Event<Action<HubConnectionStatus>>(HubConnectionStatus.Connected);

        // Should NOT re-subscribe since we unsubscribed
        await _hubClient.DidNotReceive().SubscribeToServerAsync(serverId);
    }
}
