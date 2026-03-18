using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Api.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    FennecDbContext dbContext,
    IOptions<FennecSettings> fennecSettings
) : IRequestHandler<ListUserJoinedServersQuery, IQueryable<ListUserJoinedServersResponse>>
{
    public async Task<IQueryable<ListUserJoinedServersResponse>> Handle(
        ListUserJoinedServersQuery request,
        CancellationToken cancellationToken
    )
    {
        var issuerUrl = request.AuthPrincipal.Issuer;
        var isLocal = issuerUrl == fennecSettings.Value.IssuerUrl;

        // Local users have instance_url = NULL in known_user
        var knownUser = await dbContext.Set<KnownUser>()
            .Where(x => x.RemoteId == request.AuthPrincipal.Id)
            .Where(x => isLocal ? x.InstanceUrl == null : x.InstanceUrl == issuerUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownUser is null)
        {
            return Enumerable.Empty<ListUserJoinedServersResponse>().AsQueryable();
        }

        if (isLocal)
        {
            // Local user: return servers they're a member of on this instance
            var localIssuerUrl = fennecSettings.Value.IssuerUrl;
            return dbContext.Set<ServerMember>()
                .Where(x => x.KnownUserId == knownUser.Id)
                .Select(x => new ListUserJoinedServersResponse
                {
                    InstanceUrl = localIssuerUrl,
                    ServerId = x.ServerId,
                    Name = x.Server.Name,
                });
        }

        // Remote user: return known servers they've joined via federation
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
