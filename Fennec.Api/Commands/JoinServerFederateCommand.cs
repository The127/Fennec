using Fennec.Api.Controllers.FederationApi;
using Fennec.Api.Models;
using Fennec.Api.Services;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record JoinServerFederateCommand : IRequest<JoinServerFederateResponse>
{
    public required string InviteCode { get; init; }
    public required RemoteUserInfoDto UserInfo { get; init; }
    public required string InstanceUrl { get; init; }
}

public record JoinServerFederateResponse
{
    public required string Name { get; init; }
    public required Guid ServerId { get; init; }
}

public class JoinServerFederateCommandHandler(
    FennecDbContext dbContext,
    IClockService clockService
) : IRequestHandler<JoinServerFederateCommand, JoinServerFederateResponse>
{
    public async Task<JoinServerFederateResponse> Handle(JoinServerFederateCommand request, CancellationToken cancellationToken)
    {
        var invite = await dbContext.Set<ServerInvite>()
            .SingleOrDefaultAsync(i => i.Code == request.InviteCode, cancellationToken);

        if (invite is null)
        {
            throw new HttpNotFoundException("Invite not found");
        }

        var now = clockService.GetCurrentInstant();

        if (invite.ExpiresAt is not null && invite.ExpiresAt <= now)
        {
            throw new HttpBadRequestException("Invite has expired");
        }

        if (invite.MaxUses is not null && invite.Uses >= invite.MaxUses)
        {
            throw new HttpBadRequestException("Invite has reached its maximum number of uses");
        }

        var knownUser = await dbContext.Set<KnownUser>()
            .Where(x => x.RemoteId == request.UserInfo.UserId)
            .Where(x => x.InstanceUrl == request.InstanceUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (knownUser is null)
        {
            knownUser = new KnownUser
            {
                RemoteId = request.UserInfo.UserId,
                InstanceUrl = request.InstanceUrl,
                Name = request.UserInfo.Name,
            };
            dbContext.Add(knownUser);
        }

        var alreadyMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == invite.ServerId && m.KnownUserId == knownUser.Id, cancellationToken);

        if (alreadyMember)
        {
            throw new HttpBadRequestException("Already a member of this server");
        }

        var serverName = await dbContext.Set<Server>()
            .Where(s => s.Id == invite.ServerId)
            .Select(s => s.Name)
            .SingleAsync(cancellationToken);

        invite.Uses++;
        dbContext.Update(invite);

        dbContext.Add(new ServerMember
        {
            ServerId = invite.ServerId,
            KnownUserId = knownUser.Id,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new JoinServerFederateResponse
        {
            Name = serverName,
            ServerId = invite.ServerId,
        };
    }
}
