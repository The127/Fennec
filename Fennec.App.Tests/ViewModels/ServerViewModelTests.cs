using CommunityToolkit.Mvvm.Messaging;
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
            .Returns(Task.FromResult(new List<ListChannelGroupsResponseItemDto>()));
    }

    private readonly ILogger<ServerViewModel> _logger = Substitute.For<ILogger<ServerViewModel>>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    private ServerViewModel CreateViewModel() => new(_client, new DialogManager(), _serverStore, _messageHubService, _voiceCallService, _messenger, new ToastManager(), _logger, _settingsStore, NullLoggerFactory.Instance, _serverId, "Test Server", "https://fennec.chat", Guid.NewGuid(), "testuser");

    [Fact]
    public async Task Loading_populates_channel_groups_with_channels()
    {
        var groupId = Guid.NewGuid();

        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new List<ListChannelGroupsResponseItemDto>
            {
                new() { ChannelGroupId = groupId, Name = "default group" },
            });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(new List<ListChannelsResponseItemDto>
            {
                new()
                {
                    ChannelId = Guid.NewGuid(),
                    Name = "general",
                    ChannelType = ChannelType.TextAndVoice,
                    ChannelGroupId = groupId,
                },
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
            .Returns(new List<ListChannelGroupsResponseItemDto>
            {
                new() { ChannelGroupId = group1Id, Name = "text" },
                new() { ChannelGroupId = group2Id, Name = "voice" },
            });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, group1Id, Arg.Any<CancellationToken>())
            .Returns(new List<ListChannelsResponseItemDto>
            {
                new() { ChannelId = Guid.NewGuid(), Name = "general", ChannelType = ChannelType.TextOnly, ChannelGroupId = group1Id },
                new() { ChannelId = Guid.NewGuid(), Name = "random", ChannelType = ChannelType.TextOnly, ChannelGroupId = group1Id },
            });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, group2Id, Arg.Any<CancellationToken>())
            .Returns(new List<ListChannelsResponseItemDto>
            {
                new() { ChannelId = Guid.NewGuid(), Name = "lounge", ChannelType = ChannelType.TextAndVoice, ChannelGroupId = group2Id },
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
            .Returns(new List<ListChannelGroupsResponseItemDto> { new() { ChannelGroupId = groupId, Name = "default" } });

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(new List<ListChannelsResponseItemDto>
            {
                new() { ChannelId = channelId, Name = "general", ChannelType = ChannelType.TextAndVoice, ChannelGroupId = groupId },
            });

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.NotNull(vm.SelectedChannel);
        Assert.Equal("general", vm.SelectedChannel!.Name);
    }

    [Fact]
    public async Task Api_failure_leaves_channel_groups_empty()
    {
        _serverStore.GetChannelGroupsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new List<ListChannelGroupsResponseItemDto>());

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
            .Returns(Task.FromResult(new List<ListChannelGroupsResponseItemDto>
            {
                new() { ChannelGroupId = groupId, Name = "stored group" }
            }));

        _serverStore.GetChannelsAsync(Arg.Any<string>(), Arg.Any<IFennecClient>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ListChannelsResponseItemDto>
            {
                new() { ChannelId = channelId, Name = "stored channel", ChannelType = ChannelType.TextOnly, ChannelGroupId = groupId }
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
