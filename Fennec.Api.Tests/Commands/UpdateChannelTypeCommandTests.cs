using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class UpdateChannelTypeCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _knownUserId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _issuer = "https://fennec.example.com";

    public UpdateChannelTypeCommandTests()
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

    private UpdateChannelTypeCommandHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Member_can_update_channel_type_to_text_only()
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

        var memberSet = new List<ServerMember> { new() { ServerId = _serverId, KnownUserId = _knownUserId } }.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);

        var command = new UpdateChannelTypeCommand
        {
            ChannelId = channel.Id,
            ChannelType = ChannelType.TextOnly,
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal(ChannelType.TextOnly, channel.ChannelType);
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_update_channel_type()
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

        var command = new UpdateChannelTypeCommand
        {
            ChannelId = channel.Id,
            ChannelType = ChannelType.TextOnly,
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Updating_nonexistent_channel_throws_not_found()
    {
        var channelSet = new List<Channel>().BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);

        var command = new UpdateChannelTypeCommand
        {
            ChannelId = Guid.NewGuid(),
            ChannelType = ChannelType.TextOnly,
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
