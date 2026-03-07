using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record JoinServerCommand : IRequest
{
    public required Guid ServerId { get; init; }
    public required IAuthPrinciple AuthPrincipal { get; init; }
}


public class JoinServerCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<JoinServerCommand>
{
    public async Task Handle(JoinServerCommand request, CancellationToken cancellationToken)
    {
        var serverInfo = await dbContext
            .Set<Server>()
            .Where(s => s.Id == request.ServerId)
            .Select(s => new { s.Visibility })
            .SingleOrDefaultAsync(cancellationToken);

        if (serverInfo == null)
        {
            throw new HttpNotFoundException("Server not found");
        }

        if (serverInfo.Visibility != ServerVisibility.Public)
        {
            throw new HttpBadRequestException("Server not public");
        }

        var member = new ServerMember
        {
            ServerId = request.ServerId,
            UserId = request.AuthPrincipal.Id,
        };
        
        dbContext.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}