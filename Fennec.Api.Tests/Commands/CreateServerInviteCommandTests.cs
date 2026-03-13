using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class CreateServerInviteCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _knownUserId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _issuer = "https://fennec.example.com";

    public CreateServerInviteCommandTests()
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

    private CreateServerInviteCommandHandler CreateHandler() => new(_dbContext);

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
    public async Task Member_can_create_invite_for_their_server()
    {
        SetupMembership(exists: true);

        var command = new CreateServerInviteCommand
        {
            ServerId = _serverId,
            AuthPrincipal = _authPrincipal,
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.InviteId);
        Assert.Equal(8, result.Code.Length);
        Assert.True(result.Code.All(char.IsLetterOrDigit));

        _dbContext.Received().Add(Arg.Is<ServerInvite>(i =>
            i.ServerId == _serverId &&
            i.CreatedByKnownUserId == _knownUserId &&
            i.Code.Length == 8));
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_member_cannot_create_invite()
    {
        SetupMembership(exists: false);

        var command = new CreateServerInviteCommand
        {
            ServerId = _serverId,
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Invite_respects_expiry_and_max_uses()
    {
        SetupMembership(exists: true);
        var expiresAt = Instant.FromUtc(2026, 6, 1, 0, 0);

        var command = new CreateServerInviteCommand
        {
            ServerId = _serverId,
            AuthPrincipal = _authPrincipal,
            ExpiresAt = expiresAt,
            MaxUses = 10,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        _dbContext.Received().Add(Arg.Is<ServerInvite>(i =>
            i.ExpiresAt == expiresAt &&
            i.MaxUses == 10));
    }
}
