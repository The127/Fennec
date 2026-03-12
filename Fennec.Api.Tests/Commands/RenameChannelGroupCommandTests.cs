using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class RenameChannelGroupCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public RenameChannelGroupCommandTests()
    {
        _authPrincipal.Id.Returns(_userId);
        _authPrincipal.Name.Returns("alice");
    }

    private RenameChannelGroupCommandHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Member_can_rename_channel_group()
    {
        var group = new ChannelGroup
        {
            Name = "old name",
            ServerId = _serverId,
        };

        var groupSet = new List<ChannelGroup> { group }.BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);

        var memberSet = new List<ServerMember> { new() { ServerId = _serverId, UserId = _userId } }.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);

        var command = new RenameChannelGroupCommand
        {
            ChannelGroupId = group.Id,
            NewName = "new name",
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal("new name", group.Name);
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_rename_channel_group()
    {
        var group = new ChannelGroup
        {
            Name = "voice",
            ServerId = _serverId,
        };

        var groupSet = new List<ChannelGroup> { group }.BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);

        var memberSet = new List<ServerMember>().BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);

        var command = new RenameChannelGroupCommand
        {
            ChannelGroupId = group.Id,
            NewName = "new name",
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Renaming_nonexistent_channel_group_throws_not_found()
    {
        var groupSet = new List<ChannelGroup>().BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);

        var command = new RenameChannelGroupCommand
        {
            ChannelGroupId = Guid.NewGuid(),
            NewName = "new name",
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
