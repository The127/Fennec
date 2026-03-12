using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record DeleteChannelCommand : IRequest
{
    public required Guid ChannelId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public class DeleteChannelCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<DeleteChannelCommand>
{
    public async Task Handle(DeleteChannelCommand request, CancellationToken cancellationToken)
    {
        var channel = await dbContext.Set<Channel>()
            .SingleOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel is null)
        {
            throw new HttpNotFoundException("Channel not found");
        }

        var isMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == channel.ServerId && m.UserId == request.AuthPrincipal.Id, cancellationToken);

        if (!isMember)
        {
            throw new HttpForbiddenException("You must be a member of the server to delete a channel");
        }

        dbContext.Remove(channel);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
