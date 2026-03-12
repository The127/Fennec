using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using MediatR;

namespace Fennec.Api.Queries;

public record ListChannelsQuery : IRequest<IQueryable<ListChannelsResponse>>
{
    public required Guid ChannelGroupId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record ListChannelsResponse
{
    public required Guid ChannelId { get; init; }
    public required string Name { get; init; }
    public required ChannelType ChannelType { get; init; }
    public required Guid ChannelGroupId { get; init; }
}

public class ListChannelsQueryHandler(
    FennecDbContext dbContext
) : IRequestHandler<ListChannelsQuery, IQueryable<ListChannelsResponse>>
{
    public Task<IQueryable<ListChannelsResponse>> Handle(ListChannelsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Channel>()
            .Where(c => c.ChannelGroupId == request.ChannelGroupId)
            .Select(c => new ListChannelsResponse
            {
                ChannelId = c.Id,
                Name = c.Name,
                ChannelType = c.ChannelType,
                ChannelGroupId = c.ChannelGroupId,
            });

        return Task.FromResult(query);
    }
}
