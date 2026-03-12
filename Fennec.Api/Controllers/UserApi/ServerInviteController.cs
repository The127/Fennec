using Fennec.Api.Commands;
using Fennec.Api.Queries;
using Fennec.Api.Security;
using Fennec.Shared.Dtos.Server;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace Fennec.Api.Controllers.UserApi;

[ApiController]
[UserAuth]
[Route("api/v1/servers/{serverId:guid}/invites")]
public class ServerInviteController : UserControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateInvite(
        Guid serverId,
        [FromBody] CreateServerInviteRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        Instant? expiresAt = null;
        if (requestDto.ExpiresAt is not null)
        {
            var parseResult = InstantPattern.ExtendedIso.Parse(requestDto.ExpiresAt);
            if (!parseResult.Success)
            {
                return BadRequest("Invalid ExpiresAt format");
            }
            expiresAt = parseResult.Value;
        }

        var response = await mediator.Send(new CreateServerInviteCommand
        {
            ServerId = serverId,
            AuthPrincipal = AuthPrincipal,
            ExpiresAt = expiresAt,
            MaxUses = requestDto.MaxUses,
        }, cancellationToken);

        return Ok(new CreateServerInviteResponseDto
        {
            InviteId = response.InviteId,
            Code = response.Code,
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListInvites(
        Guid serverId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var src = await mediator.Send(new ListServerInvitesQuery
        {
            ServerId = serverId,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        var items = await src.Select(i => new ListServerInvitesResponseItemDto
        {
            InviteId = i.InviteId,
            Code = i.Code,
            CreatedByUserId = i.CreatedByUserId,
            ExpiresAt = i.ExpiresAt != null ? InstantPattern.ExtendedIso.Format(i.ExpiresAt.Value) : null,
            MaxUses = i.MaxUses,
            Uses = i.Uses,
            CreatedAt = InstantPattern.ExtendedIso.Format(i.CreatedAt),
        }).ToListAsync(cancellationToken);

        return Ok(new ListServerInvitesResponseDto
        {
            Invites = items,
        });
    }

    [HttpDelete("{inviteId:guid}")]
    public async Task<IActionResult> DeleteInvite(
        Guid serverId,
        Guid inviteId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new DeleteServerInviteCommand
        {
            InviteId = inviteId,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return NoContent();
    }
}
