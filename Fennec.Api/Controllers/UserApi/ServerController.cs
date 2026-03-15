using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Queries;
using Fennec.Api.Security;
using Fennec.Shared.Dtos.Server;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Controllers.UserApi;

[ApiController]
[UserAuth]
[Route("api/v1/servers")]
public class ServerController : UserControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> CreateServer(
        [FromBody] CreateServerRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var createServerResponse = await mediator.Send(new CreateServerCommand
        {
            Name = requestDto.Name,
            Visibility =  requestDto.Visibility,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return Ok(new CreateServerResponseDto
        {
            ServerId = createServerResponse.ServerId,
        });
    }

    [HttpPost("join")]
    public async Task<IActionResult> JoinServer(
        [FromBody] JoinServerRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new JoinServerCommand
        {
            InviteCode = requestDto.InviteCode,
            InstanceUrl = requestDto.InstanceUrl,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return NoContent();
    }

    [HttpGet("{serverId}/members")]
    public async Task<IActionResult> ListServerMembers(
        [FromRoute] Guid serverId,
        [FromServices] FennecDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var members = await dbContext.Set<ServerMember>()
            .Where(m => m.ServerId == serverId)
            .Where(m => !m.KnownUser.IsDeleted)
            .Select(m => new ListServerMembersResponseItemDto { Name = m.KnownUser.Name, InstanceUrl = m.KnownUser.InstanceUrl })
            .ToListAsync(cancellationToken);

        return Ok(new ListServerMembersResponseDto { Members = members });
    }

    [HttpGet("joined")]
    public async Task<IActionResult> ListJoinedServers(
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var src = await mediator.Send(new ListUserJoinedServersQuery
        {
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        var query = src.Select(x => new ListJoinedServersResponseItemDto
        {
            Id = x.ServerId,
            Name = x.Name,
            InstanceUrl = x.InstanceUrl,
        });

        return Ok(new ListJoinedServersResponseDto
        {
            Servers = await query.ToListAsync(cancellationToken),
        });
    }
}
