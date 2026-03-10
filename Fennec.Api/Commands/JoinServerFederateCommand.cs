using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.Models;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public class JoinServerFederateCommand : IRequest
{
    public required Guid ServerId { get; init; }
    public required RemoteUserInfoDto UserInfo { get; init; }
}

public class JoinServerFederateCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<JoinServerFederateCommand>
{
    public async Task Handle(JoinServerFederateCommand request, CancellationToken cancellationToken)
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
        
        // TODO: create user if remote and missing
        
        var member = new ServerMember
        {
            ServerId = request.ServerId,
            UserId = Guid.Empty,
        };
        
        dbContext.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}