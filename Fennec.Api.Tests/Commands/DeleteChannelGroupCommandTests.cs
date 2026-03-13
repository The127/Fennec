using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class DeleteChannelGroupCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _knownUserId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _issuer = "https://fennec.example.com";

    public DeleteChannelGroupCommandTests()
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

    private DeleteChannelGroupCommandHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Member_can_delete_channel_group()
    {
        var group = new ChannelGroup
        {
            Name = "voice",
            ServerId = _serverId,
        };

        var groupSet = new List<ChannelGroup> { group }.BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);

        var memberSet = new List<ServerMember> { new() { ServerId = _serverId, KnownUserId = _knownUserId } }.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);

        var command = new DeleteChannelGroupCommand
        {
            ChannelGroupId = group.Id,
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        _dbContext.Received().Remove(group);
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_delete_channel_group()
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

        var command = new DeleteChannelGroupCommand
        {
            ChannelGroupId = group.Id,
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_nonexistent_channel_group_throws_not_found()
    {
        var groupSet = new List<ChannelGroup>().BuildMockDbSet();
        _dbContext.Set<ChannelGroup>().Returns(groupSet);

        var command = new DeleteChannelGroupCommand
        {
            ChannelGroupId = Guid.NewGuid(),
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
