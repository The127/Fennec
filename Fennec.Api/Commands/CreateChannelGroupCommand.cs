using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record CreateChannelGroupCommand : IRequest<CreateChannelGroupResponse>
{
    public required Guid ServerId { get; init; }
    public required string Name { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record CreateChannelGroupResponse
{
    public required Guid ChannelGroupId { get; init; }
}

public class CreateChannelGroupCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<CreateChannelGroupCommand, CreateChannelGroupResponse>
{
    public async Task<CreateChannelGroupResponse> Handle(CreateChannelGroupCommand request, CancellationToken cancellationToken)
    {
        var knownUser = await dbContext.Set<KnownUser>()
            .Where(x => x.RemoteId == request.AuthPrincipal.Id)
            .Where(x => x.InstanceUrl == request.AuthPrincipal.Issuer)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownUser is null)
        {
            throw new HttpForbiddenException("You must be a member of the server to create a channel group");
        }

        var isMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == request.ServerId && m.KnownUserId == knownUser.Id, cancellationToken);

        if (!isMember)
        {
            throw new HttpForbiddenException("You must be a member of the server to create a channel group");
        }

        var group = new ChannelGroup
        {
            Name = request.Name,
            ServerId = request.ServerId,
        };

        dbContext.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateChannelGroupResponse
        {
            ChannelGroupId = group.Id,
        };
    }
}
