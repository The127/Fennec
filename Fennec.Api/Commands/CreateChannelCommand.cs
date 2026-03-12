using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record CreateChannelCommand : IRequest<CreateChannelResponse>
{
    public required Guid ChannelGroupId { get; init; }
    public required string Name { get; init; }
    public ChannelType ChannelType { get; init; } = ChannelType.TextAndVoice;
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record CreateChannelResponse
{
    public required Guid ChannelId { get; init; }
}

public class CreateChannelCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<CreateChannelCommand, CreateChannelResponse>
{
    public async Task<CreateChannelResponse> Handle(CreateChannelCommand request, CancellationToken cancellationToken)
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
            throw new HttpForbiddenException("You must be a member of the server to create a channel");
        }

        var channel = new Channel
        {
            Name = request.Name,
            ServerId = group.ServerId,
            ChannelGroupId = request.ChannelGroupId,
            ChannelType = request.ChannelType,
        };

        dbContext.Add(channel);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateChannelResponse
        {
            ChannelId = channel.Id,
        };
    }
}
