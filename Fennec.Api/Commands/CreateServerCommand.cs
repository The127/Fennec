using EntityFramework.Exceptions.Common;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Api.Settings;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        var issuerUrl = fennecSettings.Value.IssuerUrl;
        if (request.AuthPrincipal.Issuer != issuerUrl)
        {
            throw new UnauthorizedAccessException("Only local users can create servers");
        }
        
        var server = new Server
        {
            Name = request.Name,
            Visibility = request.Visibility,
        };

        var knownUser = await dbContext.Set<KnownUser>()
            .Where(x => x.RemoteId == request.AuthPrincipal.Id)
            .Where(x => x.InstanceUrl == issuerUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownUser is null)
        {
            knownUser = new KnownUser
            {
                RemoteId = request.AuthPrincipal.Id,
                InstanceUrl = issuerUrl,
                Name = request.AuthPrincipal.Name,
            };
            dbContext.Add(knownUser);
        }
        
        var member = new ServerMember
        {
            KnownUserId = knownUser.Id,
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
            ChannelType = ChannelType.TextAndVoice,
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
            KnownUserId = knownUser.Id,
        };

        dbContext.AddRange(server, member, defaultGroup, defaultChannel, knownServer, joinedKnownServer);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            throw new HttpBadRequestException("Server name already taken");
        }
        
        return new CreateServerResponse
        {
            ServerId = server.Id,
        };
    }
}