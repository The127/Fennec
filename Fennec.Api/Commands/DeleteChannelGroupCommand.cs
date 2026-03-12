using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record DeleteChannelGroupCommand : IRequest
{
    public required Guid ChannelGroupId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public class DeleteChannelGroupCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<DeleteChannelGroupCommand>
{
    public async Task Handle(DeleteChannelGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await dbContext.Set<ChannelGroup>()
            .SingleOrDefaultAsync(g => g.Id == request.ChannelGroupId, cancellationToken);

        if (group is null)
        {
            throw new HttpNotFoundException("Channel group not found");
        }

        var isMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == group.ServerId && m.UserId == request.AuthPrincipal.Id, cancellationToken);

        if (!isMember)
        {
            throw new HttpForbiddenException("You must be a member of the server to delete a channel group");
        }

        dbContext.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
