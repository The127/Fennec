using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class CreateChannelCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateChannelCommandTests()
    {
        _authPrincipal.Id.Returns(_userId);
        _authPrincipal.Name.Returns("alice");
    }

    private CreateChannelCommandHandler CreateHandler() => new(_dbContext);

    private ChannelGroup SetupGroup()
    {
        var group = new ChannelGroup { Name = "general", ServerId = _serverId };
        var groupSet = new List<ChannelGroup> { group }.BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);
        return group;
    }

    private void SetupMembership(bool exists)
    {
        var members = exists
            ? new List<ServerMember> { new() { ServerId = _serverId, UserId = _userId } }
            : new List<ServerMember>();
        var memberSet = members.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);
    }

    [Fact]
    public async Task Member_can_create_channel_in_group()
    {
        var group = SetupGroup();
        SetupMembership(exists: true);

        var command = new CreateChannelCommand
        {
            ChannelGroupId = group.Id,
            Name = "chat",
            AuthPrincipal = _authPrincipal,
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ChannelId);

        _dbContext.Received().Add(Arg.Is<Channel>(c =>
            c.Name == "chat" &&
            c.ChannelGroupId == group.Id &&
            c.ServerId == _serverId &&
            c.ChannelType == ChannelType.TextAndVoice));
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_create_channel()
    {
        var group = SetupGroup();
        SetupMembership(exists: false);

        var command = new CreateChannelCommand
        {
            ChannelGroupId = group.Id,
            Name = "chat",
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Creating_channel_in_nonexistent_group_throws_not_found()
    {
        var groupSet = new List<ChannelGroup>().BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);

        var command = new CreateChannelCommand
        {
            ChannelGroupId = Guid.NewGuid(),
            Name = "chat",
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Can_create_text_only_channel()
    {
        var group = SetupGroup();
        SetupMembership(exists: true);

        var command = new CreateChannelCommand
        {
            ChannelGroupId = group.Id,
            Name = "announcements",
            ChannelType = ChannelType.TextOnly,
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        _dbContext.Received().Add(Arg.Is<Channel>(c =>
            c.ChannelType == ChannelType.TextOnly));
    }
}
