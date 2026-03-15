using Fennec.Api.Hubs;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Api.Services;
using Fennec.Shared.Dtos.Federation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Controllers.FederationApi;

[ApiController]
[Route("federation/v1/presence")]
[FederationAuth]
public class FederationPresenceController(
    FennecDbContext dbContext,
    FederatedPresenceCache presenceCache,
    IHubContext<MessageHub> hubContext
) : FederationControllerBase
{
    [HttpPost("push")]
    public async Task<IActionResult> PushPresence([FromBody] FederationPresencePushRequestDto request, CancellationToken cancellationToken)
    {
        var instanceUrl = AuthPrincipal.Issuer;

        presenceCache.SetPresence(request.UserId, instanceUrl, request.Username, request.IsOnline);

        var knownUser = await dbContext.Set<KnownUser>()
            .FirstOrDefaultAsync(k => k.RemoteId == request.UserId && k.InstanceUrl == instanceUrl, cancellationToken);

        if (knownUser is null)
            return Ok();

        var serverIds = await dbContext.Set<ServerMember>()
            .Where(sm => sm.KnownUserId == knownUser.Id)
            .Select(sm => sm.ServerId)
            .ToListAsync(cancellationToken);

        foreach (var serverId in serverIds)
        {
            var groupName = $"server-{serverId}";
            if (request.IsOnline)
                await hubContext.Clients.Group(groupName).SendAsync("UserOnline", serverId, request.UserId, request.Username, instanceUrl, cancellationToken: cancellationToken);
            else
                await hubContext.Clients.Group(groupName).SendAsync("UserOffline", serverId, request.UserId, cancellationToken: cancellationToken);
        }

        return Ok();
    }
}
