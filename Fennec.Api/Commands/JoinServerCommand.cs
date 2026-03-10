using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.FederationClient;
using Fennec.Api.Models;
using Fennec.Api.Security;
using MediatR;

namespace Fennec.Api.Commands;

public record JoinServerCommand : IRequest
{
    public required Guid ServerId { get; init; }
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
        await federationClient.For(request.InstanceUrl)
            .Server.JoinServerAsync(new FederationServerController.ServerJoinFederateRequestDto
            {
                ServerId = request.ServerId,
                UserInfo = new RemoteUserInfoDto
                {
                    UserId = request.AuthPrincipal.Id,
                    Name = request.AuthPrincipal.Name,
                },
            }, cancellationToken);
    }
}