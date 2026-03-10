using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Api.Settings;
using Fennec.Shared.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Fennec.Api.Commands;

public record CreateServerCommand : IRequest<CreateServerResponse>
{
    public required string Name { get; init; }
    public required ServerVisibility Visibility { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
}

public record CreateServerResponse
{
    public required Guid ServerId { get; init; }   
}

public class CreateServerCommandHandler(
    FennecDbContext dbContext,
    IOptions<FennecSettings> fennecSettings 
) : IRequestHandler<CreateServerCommand, CreateServerResponse>
{
    public async Task<CreateServerResponse> Handle(CreateServerCommand request, CancellationToken cancellationToken)
    {
        if (!request.AuthPrincipal.IsLocal)
        {
            throw new UnauthorizedAccessException("Only local users can create servers");
        }
        
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

        var knownServer = new KnownServer
        {
            InstanceUrl = fennecSettings.Value.IssuerUrl,
            RemoteId = server.Id,
            Name = server.Name,
        };

        var joinedKnownServer = new UserJoinedKnownServer
        {
            KnownServerId = knownServer.Id,
            UserId = request.AuthPrincipal.Id,
        };

        dbContext.AddRange(server, member, defaultGroup, defaultChannel, knownServer, joinedKnownServer);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new CreateServerResponse
        {
            ServerId = server.Id,
        };
    }
}