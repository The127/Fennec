using Fennec.Api.Models;
using Fennec.Api.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Queries;

public record ListUserJoinedServersQuery : IRequest<IQueryable<ListUserJoinedServersResponse>>
{
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record ListUserJoinedServersResponse
{
    public required Guid ServerId { get; init; }
    public required string InstanceUrl { get; init; }
    public required string Name { get; init; }   
}

public class ListUserJoinedServersQueryHandler(
    FennecDbContext dbContext
) : IRequestHandler<ListUserJoinedServersQuery, IQueryable<ListUserJoinedServersResponse>>
{
    public async Task<IQueryable<ListUserJoinedServersResponse>> Handle(
        ListUserJoinedServersQuery request,
        CancellationToken cancellationToken
    )
    {
        var issuerUrl = request.AuthPrincipal.Issuer;
        
        var knownUser = await dbContext.Set<KnownUser>()
            .Where(x => x.RemoteId == request.AuthPrincipal.Id)
            .Where(x => x.InstanceUrl == issuerUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownUser is null)
        {
            return Enumerable.Empty<ListUserJoinedServersResponse>().AsQueryable();
        }

        return dbContext.Set<UserJoinedKnownServer>()
            .Where(x => x.KnownUserId == knownUser.Id)
            .Select(x => new ListUserJoinedServersResponse
            {
                InstanceUrl = x.KnownServer.InstanceUrl,
                ServerId = x.KnownServer.RemoteId,
                Name = x.KnownServer.Name,
            });
    }
}