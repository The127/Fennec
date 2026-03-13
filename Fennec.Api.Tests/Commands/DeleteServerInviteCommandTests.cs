using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class DeleteServerInviteCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _knownUserId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _issuer = "https://fennec.example.com";

    public DeleteServerInviteCommandTests()
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

    private DeleteServerInviteCommandHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Creator_can_delete_their_invite()
    {
        var invite = new ServerInvite
        {
            ServerId = Guid.NewGuid(),
            Code = "aBcD1234",
            CreatedByKnownUserId = _knownUserId,
        };

        var mockSet = new List<ServerInvite> { invite }.BuildMockDbSet();
        _dbContext.Set<ServerInvite>().Returns(mockSet);

        var command = new DeleteServerInviteCommand
        {
            InviteId = invite.Id,
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        _dbContext.Received().Remove(invite);
        await _dbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_creator_cannot_delete_invite()
    {
        var invite = new ServerInvite
        {
            ServerId = Guid.NewGuid(),
            Code = "aBcD1234",
            CreatedByKnownUserId = Guid.NewGuid(), // different user
        };

        var mockSet = new List<ServerInvite> { invite }.BuildMockDbSet();
        _dbContext.Set<ServerInvite>().Returns(mockSet);

        var command = new DeleteServerInviteCommand
        {
            InviteId = invite.Id,
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpForbiddenException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_nonexistent_invite_throws_not_found()
    {
        var mockSet = new List<ServerInvite>().BuildMockDbSet();
        _dbContext.Set<ServerInvite>().Returns(mockSet);

        var command = new DeleteServerInviteCommand
        {
            InviteId = Guid.NewGuid(),
            AuthPrincipal = _authPrincipal,
        };

        await Assert.ThrowsAsync<HttpNotFoundException>(
            () => CreateHandler().Handle(command, CancellationToken.None));
    }
}
