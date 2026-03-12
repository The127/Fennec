using Fennec.Api.Commands;
using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.FederationClient;
using Fennec.Api.FederationClient.Clients;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Fennec.Api.Tests.Commands;

public class JoinServerCommandTests
{
    private readonly FennecDbContext _dbContext = Substitute.For<FennecDbContext>(
        new DbContextOptionsBuilder<FennecDbContext>().Options);

    private readonly IFederationClient _federationClient = Substitute.For<IFederationClient>();
    private readonly ITargetedFederationClient _targetedClient = Substitute.For<ITargetedFederationClient>();
    private readonly IServerClient _serverClient = Substitute.For<IServerClient>();
    private readonly IAuthPrincipal _authPrincipal = Substitute.For<IAuthPrincipal>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _remoteServerId = Guid.NewGuid();

    public JoinServerCommandTests()
    {
        _authPrincipal.Id.Returns(_userId);
        _authPrincipal.Name.Returns("alice");

        _federationClient.For(Arg.Any<string>()).Returns(_targetedClient);
        _targetedClient.Server.Returns(_serverClient);
    }

    private JoinServerCommandHandler CreateHandler() => new(_dbContext, _federationClient);

    [Fact]
    public async Task Joining_via_invite_calls_federation_redeem()
    {
        _serverClient.RedeemInviteAsync(
                Arg.Any<FederationServerController.ServerRedeemInviteFederateRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(new FederationServerController.ServerRedeemInviteFederateResponseDto
            {
                ServerId = _remoteServerId,
                Name = "Remote Server",
            });

        var knownServers = new List<KnownServer>().BuildMockDbSet();
        _dbContext.Set<KnownServer>().Returns(knownServers);

        var command = new JoinServerCommand
        {
            InviteCode = "aBcD1234",
            InstanceUrl = "remote.fennec.chat",
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        await _serverClient.Received().RedeemInviteAsync(
            Arg.Is<FederationServerController.ServerRedeemInviteFederateRequestDto>(r =>
                r.InviteCode == "aBcD1234" &&
                r.UserInfo.UserId == _userId),
            Arg.Any<CancellationToken>());

        _federationClient.Received().For("https://remote.fennec.chat");

        _dbContext.Received().Add(Arg.Is<KnownServer>(k =>
            k.RemoteId == _remoteServerId &&
            k.InstanceUrl == "https://remote.fennec.chat" &&
            k.Name == "Remote Server"));

        _dbContext.Received().Add(Arg.Is<UserJoinedKnownServer>(j =>
            j.UserId == _userId));
    }

    [Fact]
    public async Task Existing_known_server_is_reused()
    {
        var existingKnownServer = new KnownServer
        {
            RemoteId = _remoteServerId,
            InstanceUrl = "https://remote.fennec.chat",
            Name = "Remote Server",
        };

        _serverClient.RedeemInviteAsync(
                Arg.Any<FederationServerController.ServerRedeemInviteFederateRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(new FederationServerController.ServerRedeemInviteFederateResponseDto
            {
                ServerId = _remoteServerId,
                Name = "Remote Server",
            });

        var knownServers = new List<KnownServer> { existingKnownServer }.BuildMockDbSet();
        _dbContext.Set<KnownServer>().Returns(knownServers);

        var command = new JoinServerCommand
        {
            InviteCode = "aBcD1234",
            InstanceUrl = "remote.fennec.chat",
            AuthPrincipal = _authPrincipal,
        };

        await CreateHandler().Handle(command, CancellationToken.None);

        _dbContext.DidNotReceive().Add(Arg.Any<KnownServer>());
        _dbContext.Received().Add(Arg.Is<UserJoinedKnownServer>(j =>
            j.UserId == _userId &&
            j.KnownServerId == existingKnownServer.Id));
    }
}
