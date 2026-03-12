using Fennec.Api.Models;
using Fennec.Api.Queries;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Queries;

public class ListChannelsQueryTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _channelGroupId = Guid.NewGuid();

    private ListChannelsQueryHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Returns_channels_for_channel_group()
    {
        var otherGroupId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var channels = new List<Channel>
        {
            new() { Name = "chat", ServerId = serverId, ChannelGroupId = _channelGroupId, ChannelType = ChannelType.TextAndVoice },
            new() { Name = "announcements", ServerId = serverId, ChannelGroupId = _channelGroupId, ChannelType = ChannelType.TextOnly },
            new() { Name = "other", ServerId = serverId, ChannelGroupId = otherGroupId, ChannelType = ChannelType.TextAndVoice },
        };

        var channelSet = channels.BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);

        var query = new ListChannelsQuery
        {
            ChannelGroupId = _channelGroupId,
            AuthPrincipal = _authPrincipal,
        };

        var result = await CreateHandler().Handle(query, CancellationToken.None);
        var items = await result.ToListAsync();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, c => c.Name == "chat" && c.ChannelType == ChannelType.TextAndVoice);
        Assert.Contains(items, c => c.Name == "announcements" && c.ChannelType == ChannelType.TextOnly);
    }
}
