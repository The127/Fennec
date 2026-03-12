using Fennec.App.ViewModels;
using Fennec.Client;
using Fennec.Client.Clients;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class ServerViewModelTests
{
    private readonly IFennecClient _client = Substitute.For<IFennecClient>();
    private readonly IServerClient _serverClient = Substitute.For<IServerClient>();
    private readonly Guid _serverId = Guid.NewGuid();

    public ServerViewModelTests()
    {
        _client.Server.Returns(_serverClient);
    }

    private ServerViewModel CreateViewModel() => new(_client, new DialogManager(), _serverId, "Test Server", "https://fennec.chat");

    [Fact]
    public async Task Loading_populates_channel_groups_with_channels()
    {
        var groupId = Guid.NewGuid();

        _serverClient.ListChannelGroupsAsync(Arg.Any<string>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new ListChannelGroupsResponseDto
            {
                ChannelGroups =
                [
                    new ListChannelGroupsResponseItemDto { ChannelGroupId = groupId, Name = "default group" },
                ],
            });

        _serverClient.ListChannelsAsync(Arg.Any<string>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(new ListChannelsResponseDto
            {
                Channels =
                [
                    new ListChannelsResponseItemDto
                    {
                        ChannelId = Guid.NewGuid(),
                        Name = "general",
                        ChannelType = ChannelType.TextAndVoice,
                        ChannelGroupId = groupId,
                    },
                ],
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

        _serverClient.ListChannelGroupsAsync(Arg.Any<string>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new ListChannelGroupsResponseDto
            {
                ChannelGroups =
                [
                    new ListChannelGroupsResponseItemDto { ChannelGroupId = group1Id, Name = "text" },
                    new ListChannelGroupsResponseItemDto { ChannelGroupId = group2Id, Name = "voice" },
                ],
            });

        _serverClient.ListChannelsAsync(Arg.Any<string>(), _serverId, group1Id, Arg.Any<CancellationToken>())
            .Returns(new ListChannelsResponseDto
            {
                Channels =
                [
                    new ListChannelsResponseItemDto { ChannelId = Guid.NewGuid(), Name = "general", ChannelType = ChannelType.TextOnly, ChannelGroupId = group1Id },
                    new ListChannelsResponseItemDto { ChannelId = Guid.NewGuid(), Name = "random", ChannelType = ChannelType.TextOnly, ChannelGroupId = group1Id },
                ],
            });

        _serverClient.ListChannelsAsync(Arg.Any<string>(), _serverId, group2Id, Arg.Any<CancellationToken>())
            .Returns(new ListChannelsResponseDto
            {
                Channels =
                [
                    new ListChannelsResponseItemDto { ChannelId = Guid.NewGuid(), Name = "lounge", ChannelType = ChannelType.TextAndVoice, ChannelGroupId = group2Id },
                ],
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

        _serverClient.ListChannelGroupsAsync(Arg.Any<string>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(new ListChannelGroupsResponseDto
            {
                ChannelGroups = [new ListChannelGroupsResponseItemDto { ChannelGroupId = groupId, Name = "default" }],
            });

        _serverClient.ListChannelsAsync(Arg.Any<string>(), _serverId, groupId, Arg.Any<CancellationToken>())
            .Returns(new ListChannelsResponseDto
            {
                Channels =
                [
                    new ListChannelsResponseItemDto { ChannelId = channelId, Name = "general", ChannelType = ChannelType.TextAndVoice, ChannelGroupId = groupId },
                ],
            });

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.NotNull(vm.SelectedChannel);
        Assert.Equal("general", vm.SelectedChannel!.Name);
    }

    [Fact]
    public async Task Api_failure_leaves_channel_groups_empty()
    {
        _serverClient.ListChannelGroupsAsync(Arg.Any<string>(), _serverId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ListChannelGroupsResponseDto>(new Exception("Network error")));

        var vm = CreateViewModel();
        await vm.LoadAsync();

        Assert.Empty(vm.ChannelGroups);
    }
}
