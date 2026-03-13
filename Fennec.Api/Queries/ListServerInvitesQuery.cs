using Fennec.Api.Models;
using Fennec.Api.Security;
using MediatR;
using NodaTime;

namespace Fennec.Api.Queries;

public record ListServerInvitesQuery : IRequest<IQueryable<ListServerInvitesResponse>>
{
    public required Guid ServerId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record ListServerInvitesResponse
{
    public required Guid InviteId { get; init; }
    public required string Code { get; init; }
    public required Guid CreatedByKnownUserId { get; init; }
    public required Instant? ExpiresAt { get; init; }
    public required int? MaxUses { get; init; }
    public required int Uses { get; init; }
    public required Instant CreatedAt { get; init; }
}

public class ListServerInvitesQueryHandler(
    FennecDbContext dbContext
) : IRequestHandler<ListServerInvitesQuery, IQueryable<ListServerInvitesResponse>>
{
    public Task<IQueryable<ListServerInvitesResponse>> Handle(ListServerInvitesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ServerInvite>()
            .Where(i => i.ServerId == request.ServerId)
            .Select(i => new ListServerInvitesResponse
            {
                InviteId = i.Id,
                Code = i.Code,
                CreatedByKnownUserId = i.CreatedByKnownUserId,
                ExpiresAt = i.ExpiresAt,
                MaxUses = i.MaxUses,
                Uses = i.Uses,
                CreatedAt = i.CreatedAt,
            });

        return Task.FromResult(query);
    }
}
