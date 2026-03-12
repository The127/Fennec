using Fennec.Api.Commands;
using Fennec.Api.Queries;
using Fennec.Api.Security;
using Fennec.Shared.Dtos.Server;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Controllers.UserApi;

[ApiController]
[UserAuth]
[Route("api/v1/servers/{serverId:guid}/channel-groups")]
public class ChannelGroupController : UserControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateChannelGroup(
        Guid serverId,
        [FromBody] CreateChannelGroupRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var response = await mediator.Send(new CreateChannelGroupCommand
        {
            ServerId = serverId,
            Name = requestDto.Name,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return Ok(new CreateChannelGroupResponseDto
        {
            ChannelGroupId = response.ChannelGroupId,
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListChannelGroups(
        Guid serverId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var src = await mediator.Send(new ListChannelGroupsQuery
        {
            ServerId = serverId,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        var items = await src.Select(g => new ListChannelGroupsResponseItemDto
        {
            ChannelGroupId = g.ChannelGroupId,
            Name = g.Name,
        }).ToListAsync(cancellationToken);

        return Ok(new ListChannelGroupsResponseDto
        {
            ChannelGroups = items,
        });
    }

    [HttpPut("{channelGroupId:guid}/name")]
    public async Task<IActionResult> RenameChannelGroup(
        Guid serverId,
        Guid channelGroupId,
        [FromBody] RenameChannelGroupRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new RenameChannelGroupCommand
        {
            ChannelGroupId = channelGroupId,
            NewName = requestDto.Name,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{channelGroupId:guid}")]
    public async Task<IActionResult> DeleteChannelGroup(
        Guid serverId,
        Guid channelGroupId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new DeleteChannelGroupCommand
        {
            ChannelGroupId = channelGroupId,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return NoContent();
    }
}
