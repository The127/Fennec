using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record UpdateChannelTypeCommand : IRequest
{
    public required Guid ChannelId { get; init; }
    public required ChannelType ChannelType { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public class UpdateChannelTypeCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<UpdateChannelTypeCommand>
{
    public async Task Handle(UpdateChannelTypeCommand request, CancellationToken cancellationToken)
    {
        var channel = await dbContext.Set<Channel>()
            .SingleOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel is null)
        {
            throw new HttpNotFoundException("Channel not found");
        }

        var knownUser = await dbContext.Set<KnownUser>()
            .SingleOrDefaultAsync(u => u.RemoteId == request.AuthPrincipal.Id && u.InstanceUrl == request.AuthPrincipal.Issuer, cancellationToken);

        if (knownUser is null)
        {
            throw new HttpForbiddenException("User not found");
        }

        var isMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == channel.ServerId && m.KnownUserId == knownUser.Id, cancellationToken);

        if (!isMember)
        {
            throw new HttpForbiddenException("You must be a member of the server to update a channel");
        }

        channel.ChannelType = request.ChannelType;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
