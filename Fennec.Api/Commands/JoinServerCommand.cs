using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.FederationClient;
using Fennec.Api.Models;
using Fennec.Api.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record JoinServerCommand : IRequest
{
    public required string InviteCode { get; init; }
    public required string InstanceUrl { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public class JoinServerCommandHandler(
    FennecDbContext dbContext,
    IFederationClient federationClient
) : IRequestHandler<JoinServerCommand>
{
    public async Task Handle(JoinServerCommand request, CancellationToken cancellationToken)
    {
        var normalizedUrl = request.InstanceUrl.Contains("://")
            ? request.InstanceUrl
            : $"https://{request.InstanceUrl}";

        var redeemResponse = await federationClient.For(normalizedUrl)
            .Server.RedeemInviteAsync(new FederationServerController.ServerRedeemInviteFederateRequestDto
            {
                InviteCode = request.InviteCode,
                UserInfo = new RemoteUserInfoDto
                {
                    UserId = request.AuthPrincipal.Id,
                    Name = request.AuthPrincipal.Name,
                },
            }, cancellationToken);

        var knownUser = await dbContext.Set<KnownUser>()
            .Where(x => x.RemoteId == request.AuthPrincipal.Id)
            .Where(x => x.InstanceUrl == request.AuthPrincipal.Issuer)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownUser is null)
        {
            knownUser = new KnownUser
            {
                RemoteId = request.AuthPrincipal.Id,
                InstanceUrl = request.AuthPrincipal.Issuer,
                Name = request.AuthPrincipal.Name,
            };
            dbContext.Add(knownUser);
        }

        var knownServer = await dbContext.Set<KnownServer>()
            .Where(x => x.RemoteId == redeemResponse.ServerId)
            .Where(x => x.InstanceUrl == normalizedUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownServer is null)
        {
            knownServer = new KnownServer
            {
                RemoteId = redeemResponse.ServerId,
                InstanceUrl = normalizedUrl,
                Name = redeemResponse.Name,
            };
            dbContext.Add(knownServer);
        }

        dbContext.Add(new UserJoinedKnownServer
        {
            KnownUserId = knownUser.Id,
            KnownServerId = knownServer.Id,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
