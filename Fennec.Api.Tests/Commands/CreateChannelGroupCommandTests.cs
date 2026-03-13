using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class CreateChannelGroupCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _knownUserId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _issuer = "https://fennec.example.com";

    public CreateChannelGroupCommandTests()
    {
        _authPrincipal.Id.Returns(_userId);
        _authPrincipal.Issuer.Returns(_issuer);
        _authPrincipal.Name.Returns("alice");

        var knownUser = new KnownUser
        {
            Id = _knownUserId,
            RemoteId = _userId,
            InstanceUrl = _issuer,
            Name = "alice",
        };
        var mockUserSet = new List<KnownUser> { knownUser }.BuildMockDbSet();
        _dbContext.Set<KnownUser>().Returns(mockUserSet);
    }

    private CreateChannelGroupCommandHandler CreateHandler() => new(_dbContext);

    private void SetupMembership(bool exists)
    {
        var members = exists
            ? new List<ServerMember>
            {
                new() { ServerId = _serverId, KnownUserId = _knownUserId }
            }
            : new List<ServerMember>();

        var mockSet = members.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(mockSet);
    }

    [Fact]
    public async Task Member_can_create_channel_group()
    {
        SetupMembership(exists: true);

        var command = new CreateChannelGroupCommand
        {
            ServerId = _serverId,
            Name = "voice",
            AuthPrincipal = _authPrincipal,
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ChannelGroupId);

        _dbContext.Received().Add(Arg.Is<ChannelGroup>(g =>
            g.Name == "voice" &&
            g.ServerId == _serverId));
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_create_channel_group()
    {
        SetupMembership(exists: false);

        var command = new CreateChannelGroupCommand
        {
            ServerId = _serverId,
            Name = "voice",
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
