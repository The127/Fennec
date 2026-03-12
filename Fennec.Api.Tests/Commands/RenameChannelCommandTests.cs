using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class RenameChannelCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public RenameChannelCommandTests()
    {
        _authPrincipal.Id.Returns(_userId);
        _authPrincipal.Name.Returns("alice");
    }

    private RenameChannelCommandHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Member_can_rename_channel()
    {
        var channel = new Channel
        {
            Name = "old name",
            ServerId = _serverId,
            ChannelGroupId = Guid.NewGuid(),
            ChannelType = ChannelType.TextAndVoice,
        };

        var channelSet = new List<Channel> { channel }.BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);

        var memberSet = new List<ServerMember> { new() { ServerId = _serverId, UserId = _userId } }.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);

        var command = new RenameChannelCommand
        {
            ChannelId = channel.Id,
            NewName = "new name",
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal("new name", channel.Name);
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_rename_channel()
    {
        var channel = new Channel
        {
            Name = "chat",
            ServerId = _serverId,
            ChannelGroupId = Guid.NewGuid(),
            ChannelType = ChannelType.TextAndVoice,
        };

        var channelSet = new List<Channel> { channel }.BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);

        var memberSet = new List<ServerMember>().BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);

        var command = new RenameChannelCommand
        {
            ChannelId = channel.Id,
            NewName = "new name",
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Renaming_nonexistent_channel_throws_not_found()
    {
        var channelSet = new List<Channel>().BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);

        var command = new RenameChannelCommand
        {
            ChannelId = Guid.NewGuid(),
            NewName = "new name",
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
