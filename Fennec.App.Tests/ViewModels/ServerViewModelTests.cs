using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using Fennec.Client;
using Fennec.Client.Clients;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class ServerViewModelTests
{
    private readonly IFennecClient _client = Substitute.For<IFennecClient>();
    private readonly IServerClient _serverClient = Substitute.For<IServerClient>();
    private readonly IServerStore _serverStore = Substitute.For<IServerStore>();
    private readonly IMessageHubService _messageHubService = Substitute.For<IMessageHubService>();
    private readonly IVoiceCallService _voiceCallService = Substitute.For<IVoiceCallService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly Guid _serverId = Guid.NewGuid();

    public ServerViewModelTests()
    {
        _client.Server.Returns(_serverClient);
        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ChannelGroupSummary>()));
    }

    private readonly ILogger<ServerViewModel> _logger = Substitute.For<ILogger<ServerViewModel>>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    private ServerViewModel CreateViewModel() => new(_client, new DialogManager(), _serverStore, _messageHubService, _voiceCallService, _messenger, new ToastManager(), _logger, _settingsStore, NullLoggerFactory.Instance, _serverId, "Test Server", "https://fennec.chat", Guid.NewGuid(), "testuser");

    [Fact]
    public async Task Loading_populates_channel_groups_with_channels()
    {
        var groupId = Guid.NewGuid();

        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelGroupSummary>
            {
                new(groupId, "default group"),
            });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelSummary>
            {
                new(Guid.NewGuid(), "general", ChannelType.TextAndVoice, groupId),
            });

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.Single(vm.ChannelGroups);
        Assert.Equal("default group", vm.ChannelGroups[0].Name);
        Assert.Single(vm.ChannelGroups[0].Channels);
        Assert.Equal("general", vm.ChannelGroups[0].Channels[0].Name);
        Assert.Equal(ChannelType.TextAndVoice, vm.ChannelGroups[0].Channels[0].ChannelType);
    }

    [Fact]
    public async Task Loading_populates_multiple_groups_and_channels()
    {
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();

        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelGroupSummary>
            {
                new(group1Id, "text"),
                new(group2Id, "voice"),
            });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, group1Id, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelSummary>
            {
                new(Guid.NewGuid(), "general", ChannelType.TextOnly, group1Id),
                new(Guid.NewGuid(), "random", ChannelType.TextOnly, group1Id),
            });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, group2Id, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelSummary>
            {
                new(Guid.NewGuid(), "lounge", ChannelType.TextAndVoice, group2Id),
            });

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.Equal(2, vm.ChannelGroups.Count);
        Assert.Equal(2, vm.ChannelGroups[0].Channels.Count);
        Assert.Single(vm.ChannelGroups[1].Channels);
    }

    [Fact]
    public async Task First_channel_is_selected_after_loading()
    {
        var groupId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelGroupSummary> { new(groupId, "default") });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelSummary>
            {
                new(channelId, "general", ChannelType.TextAndVoice, groupId),
            });

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.NotNull(vm.SelectedChannel);
        Assert.Equal("general", vm.SelectedChannel!.Name);
    }

    [Theory]
    [InlineData(HubConnectionStatus.Connected)]
    [InlineData(HubConnectionStatus.Connecting)]
    [InlineData(HubConnectionStatus.Reconnecting)]
    [InlineData(HubConnectionStatus.Disconnected)]
    public void GivenInitialHubStatus_HubStatusPropertyReflectsIt(HubConnectionStatus status)
    {
        _messageHubService.CurrentStatus.Returns(status);
        var vm = CreateViewModel();
        Assert.Equal(status, vm.HubStatus);
    }

    [AvaloniaTheory]
    [InlineData(HubConnectionStatus.Connected)]
    [InlineData(HubConnectionStatus.Connecting)]
    [InlineData(HubConnectionStatus.Reconnecting)]
    [InlineData(HubConnectionStatus.Disconnected)]
    public void GivenHubStatusChanges_HubStatusPropertyUpdates(HubConnectionStatus status)
    {
        var vm = CreateViewModel();
        _messenger.Send(new HubConnectionStateChangedMessage(status));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(status, vm.HubStatus);
    }

    [Fact]
    public async Task Api_failure_leaves_channel_groups_empty()
    {
        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new List<ChannelGroupSummary>());

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.Empty(vm.ChannelGroups);
    }

    [Fact]
    public async Task Loading_uses_offline_first_storage()
    {
        var groupId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ChannelGroupSummary>
            {
                new(groupId, "stored group")
            }));

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ChannelSummary>
            {
                new(channelId, "stored channel", ChannelType.TextOnly, groupId)
            }));

        // Api fails
        _serverClient.ListChannelGroupsAsync(Arg.Any<string>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ListChannelGroupsResponseDto>(new Exception("Network error")));

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.Single(vm.ChannelGroups);
        Assert.Equal("stored group", vm.ChannelGroups[0].Name);
        Assert.Single(vm.ChannelGroups[0].Channels);
        Assert.Equal("stored channel", vm.ChannelGroups[0].Channels[0].Name);
    }
}
