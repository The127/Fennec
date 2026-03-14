using System.Text.Json;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Queries;

public record ListMessagesQuery : IRequest<IQueryable<ListMessagesResponse>>
{
    public required Guid ChannelId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record ListMessagesResponse
{
    public required Guid MessageId { get; init; }
    public required Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public string? AuthorInstanceUrl { get; init; }
    public required string CreatedAt { get; init; }
    public required MessageType Type { get; init; }
    public required JsonDocument Details { get; init; }
}

public class ListMessagesQueryHandler(
    FennecDbContext dbContext
) : IRequestHandler<ListMessagesQuery, IQueryable<ListMessagesResponse>>
{
    public async Task<IQueryable<ListMessagesResponse>> Handle(ListMessagesQuery request, CancellationToken cancellationToken)
    {
        var channel = await dbContext.Set<Channel>()
            .SingleOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel is null)
        {
            throw new HttpNotFoundException("Channel not found");
        }

        var knownUser = await dbContext.Set<KnownUser>()
            .Where(x => x.RemoteId == request.AuthPrincipal.Id)
            .Where(x => x.InstanceUrl == request.AuthPrincipal.Issuer)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownUser is null)
        {
            throw new HttpForbiddenException("You must be a member of the server to view messages");
        }

        var isMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == channel.ServerId && m.KnownUserId == knownUser.Id, cancellationToken);

        if (!isMember)
        {
            throw new HttpForbiddenException("You must be a member of the server to view messages");
        }

        var query = dbContext.Set<ChannelMessage>()
            .Where(m => m.ChannelId == request.ChannelId)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .Select(m => new ListMessagesResponse
            {
                MessageId = m.Id,
                AuthorId = m.AuthorId,
                AuthorName = m.Author.Name, // KnownUser doesn't have DisplayName currently, but it should probably.
                AuthorInstanceUrl = m.Author.InstanceUrl,
                CreatedAt = m.CreatedAt.ToString(),
                Type = m.Type,
                Details = m.Details,
            });

        return query;
    }
}
