using Fennec.Api.Models;
using Fennec.Api.Queries;
using Fennec.Api.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Queries;

public class ListChannelGroupsQueryTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();

    private ListChannelGroupsQueryHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Returns_channel_groups_for_server()
    {
        var otherServerId = Guid.NewGuid();

        var groups = new List<ChannelGroup>
        {
            new() { Name = "general", ServerId = _serverId },
            new() { Name = "voice", ServerId = _serverId },
            new() { Name = "other", ServerId = otherServerId },
        };

        var groupSet = groups.BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);

        var query = new ListChannelGroupsQuery
        {
            ServerId = _serverId,
            AuthPrincipal = _authPrincipal,
        };

        var result = await CreateHandler().Handle(query, CancellationToken.None);
        var items = await result.ToListAsync();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, g => g.Name == "general");
        Assert.Contains(items, g => g.Name == "voice");
    }
}
