using System.Text.Json;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record SendMessageCommand : IRequest<SendMessageResponse>
{
    public required Guid ChannelId { get; init; }
    public required string Content { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record SendMessageResponse
{
    public required Guid MessageId { get; init; }
}

public class SendMessageCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<SendMessageCommand, SendMessageResponse>
{
    public async Task<SendMessageResponse> Handle(SendMessageCommand request, CancellationToken cancellationToken)
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
            knownUser = new KnownUser
            {
                RemoteId = request.AuthPrincipal.Id,
                InstanceUrl = request.AuthPrincipal.Issuer,
                Name = request.AuthPrincipal.Name,
            };
            dbContext.Add(knownUser);
        }

        var isMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == channel.ServerId && m.KnownUserId == knownUser.Id, cancellationToken);

        if (!isMember)
        {
            throw new HttpForbiddenException("You must be a member of the server to send messages");
        }

        var details = JsonSerializer.SerializeToDocument(new
        {
            content = request.Content,
        });

        var message = new ChannelMessage
        {
            ChannelId = request.ChannelId,
            AuthorId = knownUser.Id,
            Type = MessageType.Text,
            Details = details,
        };

        dbContext.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SendMessageResponse
        {
            MessageId = message.Id,
        };
    }
}
