using Fennec.Api.Models;
using Fennec.Api.Security;
using MediatR;

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
    public Task<IQueryable<ListUserJoinedServersResponse>> Handle(
        ListUserJoinedServersQuery request,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(dbContext.Set<UserJoinedKnownServer>()
            .Where(x => x.UserId == request.AuthPrincipal.Id)
            .Select(x => new ListUserJoinedServersResponse
            {
                InstanceUrl = x.KnownServer.InstanceUrl,
                ServerId = x.KnownServer.RemoteId,
                Name = x.KnownServer.Name,
            }));
    }
}