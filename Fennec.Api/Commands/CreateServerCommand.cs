using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Shared.Models;
using MediatR;

namespace Fennec.Api.Commands;

public record CreateServerCommand : IRequest
{
    public required string Name { get; init; }
    public required ServerVisibility Visibility { get; init; }
    public required IAuthPrinciple AuthPrincipal { get; init; }
}

public class CreateServerCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<CreateServerCommand>
{
    public async Task Handle(CreateServerCommand request, CancellationToken cancellationToken)
    {
        var server = new Server
        {
            Name = request.Name,
            Visibility = request.Visibility,
        };
        
        var member = new ServerMember
        {
            UserId = request.AuthPrincipal.Id,
            ServerId = server.Id,
        };

        var defaultGroup = new ChannelGroup
        {
            Name = "default group",
            ServerId = server.Id,
        };

        var defaultChannel = new Channel
        {
            Name = "default channel",
            ServerId = server.Id,
            ChannelGroupId = defaultGroup.Id,
        };
        
        dbContext.AddRange(server, member, defaultGroup, defaultChannel);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}