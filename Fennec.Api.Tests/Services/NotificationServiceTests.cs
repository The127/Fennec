using Fennec.Api.Models;
using Fennec.Api.Services;
using Fennec.Api.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.FederationClient;
using Fennec.Api.FederationClient.Clients;

namespace Fennec.Api.Tests.Services;

public class NotificationServiceTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);
    private readonly IMentionParser _mentionParser = Substitute.For<IMentionParser>();
    private readonly IFederationClient _federationClient = Substitute.For<IFederationClient>();
    private readonly IOptions<FennecSettings> _fennecSettings = Substitute.For<IOptions<FennecSettings>>();
    private readonly INotificationEventService _notificationEventService = Substitute.For<INotificationEventService>();
    private readonly IClockService _clockService = Substitute.For<IClockService>();

    private const string MyInstanceUrl = "https://my.instance";
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _channelId = Guid.NewGuid();

    public NotificationServiceTests()
    {
        _fennecSettings.Value.Returns(new FennecSettings { IssuerUrl = MyInstanceUrl });
    }

    private NotificationService CreateService() => new(
        _dbContext,
        _mentionParser,
        _federationClient,
        _fennecSettings,
        _notificationEventService,
        _clockService
    );

    [Fact]
    public async Task ProcessMessageAsync_WithLocalMention_DeliversLocally()
    {
        // Arrange
        var author = new KnownUser { Id = Guid.NewGuid(), Name = "author", RemoteId = Guid.NewGuid(), InstanceUrl = MyInstanceUrl };
        var targetUser = new KnownUser { Id = Guid.NewGuid(), Name = "target", RemoteId = Guid.NewGuid(), InstanceUrl = MyInstanceUrl };
        
        var message = new ChannelMessage
        {
            Id = Guid.NewGuid(),
            ChannelId = _channelId,
            AuthorId = author.Id,
            Author = author,
            Type = MessageType.Text,
            Details = JsonDocument.Parse("{\"content\": \"Hello @target\"}")
        };

        var channel = new Channel { Id = _channelId, ServerId = _serverId, Name = "chat", ChannelGroupId = Guid.NewGuid() };
        var member = new ServerMember { ServerId = _serverId, KnownUserId = targetUser.Id, KnownUser = targetUser };

        _mentionParser.ParseMentions("Hello @target").Returns([ "target" ]);

        var channelSet = new List<Channel> { channel }.BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);
        var memberSet = new List<ServerMember> { member }.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);
        var knownUserSet = new List<KnownUser> { targetUser }.BuildMockDbSet();
        _dbContext.Set<KnownUser>().Returns(knownUserSet);
        var notificationSet = new List<Notification>().BuildMockDbSet();
        _dbContext.Set<Notification>().Returns(notificationSet);

        // Act
        await CreateService().ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        _dbContext.Set<Notification>().Received().Add(Arg.Is<Notification>(n => 
            n.UserId == targetUser.RemoteId && 
            n.AuthorName == "author" &&
            n.ContentExcerpt == "Hello @target"));
        await _notificationEventService.Received().NotifyNotificationReceived(targetUser.RemoteId, Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMessageAsync_WithRemoteMention_CallsFederation()
    {
        // Arrange
        const string remoteInstance = "https://remote.instance";
        var author = new KnownUser { Id = Guid.NewGuid(), Name = "author", RemoteId = Guid.NewGuid(), InstanceUrl = MyInstanceUrl };
        var remoteUser = new KnownUser { Id = Guid.NewGuid(), Name = "remote", RemoteId = Guid.NewGuid(), InstanceUrl = remoteInstance };
        
        var message = new ChannelMessage
        {
            Id = Guid.NewGuid(),
            ChannelId = _channelId,
            AuthorId = author.Id,
            Author = author,
            Type = MessageType.Text,
            Details = JsonDocument.Parse("{\"content\": \"Hello @remote\"}")
        };

        var channel = new Channel { Id = _channelId, ServerId = _serverId, Name = "chat", ChannelGroupId = Guid.NewGuid() };
        var member = new ServerMember { ServerId = _serverId, KnownUserId = remoteUser.Id, KnownUser = remoteUser };

        _mentionParser.ParseMentions("Hello @remote").Returns([ "remote" ]);

        var channelSet = new List<Channel> { channel }.BuildMockDbSet();
        _dbContext.Set<Channel>().Returns(channelSet);
        var memberSet = new List<ServerMember> { member }.BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(memberSet);
        var knownUserSet = new List<KnownUser> { remoteUser }.BuildMockDbSet();
        _dbContext.Set<KnownUser>().Returns(knownUserSet);

        var targetedClient = Substitute.For<ITargetedFederationClient>();
        var notificationClient = Substitute.For<INotificationClient>();
        targetedClient.Notification.Returns(notificationClient);
        _federationClient.For(remoteInstance).Returns(targetedClient);

        // Act
        await CreateService().ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        await notificationClient.Received().PushNotificationAsync(Arg.Is<PushNotificationRequestDto>(r => 
            r.TargetUserId == remoteUser.RemoteId &&
            r.AuthorName == "author"), Arg.Any<CancellationToken>());
    }
}
