using Fennec.Api.Commands;
using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.Models;
using Fennec.Api.Services;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class JoinServerFederateCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IClockService _clockService = Substitute.For<IClockService>();

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 3, 12, 12, 0);

    public JoinServerFederateCommandTests()
    {
        _clockService.GetCurrentInstant().Returns(_now);
    }

    private JoinServerFederateCommandHandler CreateHandler() => new(_dbContext, _clockService);

    private ServerInvite CreateInvite(Instant? expiresAt = null, int? maxUses = null, int uses = 0)
    {
        return new ServerInvite
        {
            ServerId = _serverId,
            Code = "aBcD1234",
            CreatedByUserId = Guid.NewGuid(),
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            Uses = uses,
        };
    }

    private void SetupInvites(params ServerInvite[] invites)
    {
        var mockSet = invites.ToList().BuildMockDbSet();
        _dbContext.Set<ServerInvite>().Returns(mockSet);
    }

    private void SetupMembers(params ServerMember[] members)
    {
        var mockSet = members.ToList().BuildMockDbSet();
        _dbContext.Set<ServerMember>().Returns(mockSet);
    }

    private void SetupServers(params Server[] servers)
    {
        var mockSet = servers.ToList().BuildMockDbSet();
        _dbContext.Set<Server>().Returns(mockSet);
    }

    private RemoteUserInfoDto UserInfo() => new()
    {
        UserId = _userId,
        Name = "alice",
    };

    [Fact]
    public async Task Valid_invite_creates_server_member()
    {
        var invite = CreateInvite();
        var server = new Server { Id = _serverId, Name = "Test Server", Visibility = Shared.Models.ServerVisibility.Public };

        SetupInvites(invite);
        SetupMembers();
        SetupServers(server);

        var command = new JoinServerFederateCommand
        {
            InviteCode = "aBcD1234",
            UserInfo = UserInfo(),
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal("Test Server", result.Name);
        Assert.Equal(_serverId, result.ServerId);
        _dbContext.Received().Add(Arg.Is<ServerMember>(m =>
            m.ServerId == _serverId && m.UserId == _userId));
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Expired_invite_cannot_be_redeemed()
    {
        var invite = CreateInvite(expiresAt: _now - Duration.FromHours(1));
        SetupInvites(invite);

        var command = new JoinServerFederateCommand
        {
            InviteCode = "aBcD1234",
            UserInfo = UserInfo(),
        };

        await Assert.ThrowsAsync<HttpBadRequestException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Maxed_out_invite_cannot_be_redeemed()
    {
        var invite = CreateInvite(maxUses: 5, uses: 5);
        SetupInvites(invite);

        var command = new JoinServerFederateCommand
        {
            InviteCode = "aBcD1234",
            UserInfo = UserInfo(),
        };

        await Assert.ThrowsAsync<HttpBadRequestException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_invite_code_returns_not_found()
    {
        SetupInvites();

        var command = new JoinServerFederateCommand
        {
            InviteCode = "unknown1",
            UserInfo = UserInfo(),
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Already_a_member_cannot_join_again()
    {
        var invite = CreateInvite();
        var server = new Server { Id = _serverId, Name = "Test Server", Visibility = Shared.Models.ServerVisibility.Public };
        var existingMember = new ServerMember { ServerId = _serverId, UserId = _userId };

        SetupInvites(invite);
        SetupMembers(existingMember);
        SetupServers(server);

        var command = new JoinServerFederateCommand
        {
            InviteCode = "aBcD1234",
            UserInfo = UserInfo(),
        };

        await Assert.ThrowsAsync<HttpBadRequestException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Invite_with_no_expiry_or_limit_is_always_valid()
    {
        var invite = CreateInvite(expiresAt: null, maxUses: null);
        var server = new Server { Id = _serverId, Name = "Open Server", Visibility = Shared.Models.ServerVisibility.Public };

        SetupInvites(invite);
        SetupMembers();
        SetupServers(server);

        var command = new JoinServerFederateCommand
        {
            InviteCode = "aBcD1234",
            UserInfo = UserInfo(),
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal("Open Server", result.Name);
        Assert.Equal(_serverId, result.ServerId);
    }
}
