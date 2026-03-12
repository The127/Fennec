using Fennec.Api.Models;
using Fennec.Api.Security;
using MediatR;

namespace Fennec.Api.Queries;

public record ListChannelGroupsQuery : IRequest<IQueryable<ListChannelGroupsResponse>>
{
    public required Guid ServerId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record ListChannelGroupsResponse
{
    public required Guid ChannelGroupId { get; init; }
    public required string Name { get; init; }
}

public class ListChannelGroupsQueryHandler(
    FennecDbContext dbContext
) : IRequestHandler<ListChannelGroupsQuery, IQueryable<ListChannelGroupsResponse>>
{
    public Task<IQueryable<ListChannelGroupsResponse>> Handle(ListChannelGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ChannelGroup>()
            .Where(g => g.ServerId == request.ServerId)
            .Select(g => new ListChannelGroupsResponse
            {
                ChannelGroupId = g.Id,
                Name = g.Name,
            });

        return Task.FromResult(query);
    }
}
