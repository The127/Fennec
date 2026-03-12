using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record DeleteServerInviteCommand : IRequest
{
    public required Guid InviteId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public class DeleteServerInviteCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<DeleteServerInviteCommand>
{
    public async Task Handle(DeleteServerInviteCommand request, CancellationToken cancellationToken)
    {
        var invite = await dbContext.Set<ServerInvite>()
            .SingleOrDefaultAsync(i => i.Id == request.InviteId, cancellationToken);

        if (invite is null)
        {
            throw new HttpNotFoundException("Invite not found");
        }

        if (invite.CreatedByUserId != request.AuthPrincipal.Id)
        {
            throw new HttpForbiddenException("Only the creator can delete this invite");
        }

        dbContext.Remove(invite);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
