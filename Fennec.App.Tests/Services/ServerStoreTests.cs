using Fennec.App.Services;
using Fennec.Client;
using Fennec.Client.Clients;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Fennec.App.Tests.Services;

public class ServerStoreTests
{
    private readonly IServerRepository _serverRepo = Substitute.For<IServerRepository>();
    private readonly IChannelGroupRepository _groupRepo = Substitute.For<IChannelGroupRepository>();
    private readonly IChannelRepository _channelRepo = Substitute.For<IChannelRepository>();
    private readonly ILogger<ServerStore> _logger = Substitute.For<ILogger<ServerStore>>();
    private readonly IFennecClient _client = Substitute.For<IFennecClient>();
    private readonly IServerClient _serverClient = Substitute.For<IServerClient>();
    private readonly ServerStore _sut;

    public ServerStoreTests()
    {
        _client.Server.Returns(_serverClient);
        _sut = new ServerStore(_serverRepo, _groupRepo, _channelRepo, _logger);
    }

    [Fact]
    public async Task GetJoinedServersAsync_ReturnsCached_AndTriggersRefresh()
    {
        // Arrange
        var homeUrl = "https://fennec.chat";
        var cached = new List<ListJoinedServersResponseItemDto> 
        { 
            new() { Id = Guid.NewGuid(), Name = "Cached Server", InstanceUrl = homeUrl } 
        };
        var remote = new ListJoinedServersResponseDto
        {
            Servers = [new ListJoinedServersResponseItemDto { Id = Guid.NewGuid(), Name = "Remote Server", InstanceUrl = homeUrl }]
        };

        _serverRepo.GetJoinedServersAsync().Returns(cached);
        _serverClient.ListJoinedServersAsync(homeUrl).Returns(remote);

        // Act
        var result = await _sut.GetJoinedServersAsync(homeUrl, _client);

        // Assert
        Assert.Equal(cached, result);
        
        await _sut.WaitForRefreshesAsync();
        
        await _serverRepo.Received(1).SetJoinedServersAsync(Arg.Is<List<ListJoinedServersResponseItemDto>>(x => x.Count == 1 && x[0].Name == "Remote Server"));
    }

    [Fact]
    public async Task GetChannelGroupsAsync_ReturnsCached_AndTriggersRefresh()
    {
        // Arrange
        var instanceUrl = "https://fennec.chat";
        var serverId = Guid.NewGuid();
        var cached = new List<ListChannelGroupsResponseItemDto> 
        { 
            new() { ChannelGroupId = Guid.NewGuid(), Name = "Cached Group" } 
        };
        var remote = new ListChannelGroupsResponseDto
        {
            ChannelGroups = [new ListChannelGroupsResponseItemDto { ChannelGroupId = Guid.NewGuid(), Name = "Remote Group" }]
        };

        _groupRepo.GetChannelGroupsAsync(serverId).Returns(cached);
        _serverClient.ListChannelGroupsAsync(instanceUrl, serverId).Returns(remote);

        // Act
        var result = await _sut.GetChannelGroupsAsync(instanceUrl, _client, serverId);

        // Assert
        Assert.Equal(cached, result);
        
        await _sut.WaitForRefreshesAsync();
        
        await _groupRepo.Received(1).SetChannelGroupsAsync(serverId, Arg.Is<List<ListChannelGroupsResponseItemDto>>(x => x.Count == 1 && x[0].Name == "Remote Group"));
    }

    [Fact]
    public async Task GetChannelsAsync_ReturnsCached_AndTriggersRefresh()
    {
        // Arrange
        var instanceUrl = "https://fennec.chat";
        var serverId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var cached = new List<ListChannelsResponseItemDto> 
        { 
            new() { ChannelId = Guid.NewGuid(), Name = "Cached Channel", ChannelGroupId = groupId, ChannelType = ChannelType.TextOnly } 
        };
        var remote = new ListChannelsResponseDto
        {
            Channels = [new ListChannelsResponseItemDto { ChannelId = Guid.NewGuid(), Name = "Remote Channel", ChannelGroupId = groupId, ChannelType = ChannelType.TextOnly }]
        };

        _channelRepo.GetChannelsAsync(serverId, groupId).Returns(cached);
        _serverClient.ListChannelsAsync(instanceUrl, serverId, groupId).Returns(remote);

        // Act
        var result = await _sut.GetChannelsAsync(instanceUrl, _client, serverId, groupId);

        // Assert
        Assert.Equal(cached, result);
        
        await _sut.WaitForRefreshesAsync();
        
        await _channelRepo.Received(1).SetChannelsAsync(serverId, groupId, Arg.Is<List<ListChannelsResponseItemDto>>(x => x.Count == 1 && x[0].Name == "Remote Channel"));
    }
}
