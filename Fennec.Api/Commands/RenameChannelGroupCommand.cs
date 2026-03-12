using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record RenameChannelGroupCommand : IRequest
{
    public required Guid ChannelGroupId { get; init; }
    public required string NewName { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public class RenameChannelGroupCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<RenameChannelGroupCommand>
{
    public async Task Handle(RenameChannelGroupCommand request, CancellationToken cancellationToken)
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
            throw new HttpForbiddenException("You must be a member of the server to rename a channel group");
        }

        group.Name = request.NewName;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
