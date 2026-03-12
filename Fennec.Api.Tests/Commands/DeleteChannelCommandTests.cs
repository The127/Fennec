using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class DeleteChannelCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DeleteChannelCommandTests()
    {
        _authPrincipal.Id.Returns(_userId);
        _authPrincipal.Name.Returns("alice");
    }

    private DeleteChannelCommandHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Member_can_delete_channel()
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

        var memberSet = new List<ServerMember> { new() { ServerId = _serverId, UserId = _userId } }.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);

        var command = new DeleteChannelCommand
        {
            ChannelId = channel.Id,
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        _dbContext.Received().Remove(channel);
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_delete_channel()
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

        var command = new DeleteChannelCommand
        {
            ChannelId = channel.Id,
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_nonexistent_channel_throws_not_found()
    {
        var channelSet = new List<Channel>().BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);

        var command = new DeleteChannelCommand
        {
            ChannelId = Guid.NewGuid(),
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
