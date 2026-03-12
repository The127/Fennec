using Fennec.Api.Commands;
using Fennec.Api.Queries;
using Fennec.Api.Security;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Controllers.UserApi;

[ApiController]
[UserAuth]
[Route("api/v1/servers/{serverId:guid}/channel-groups/{channelGroupId:guid}/channels")]
public class ChannelController : UserControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateChannel(
        Guid serverId,
        Guid channelGroupId,
        [FromBody] CreateChannelRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var response = await mediator.Send(new CreateChannelCommand
        {
            ChannelGroupId = channelGroupId,
            Name = requestDto.Name,
            ChannelType = requestDto.ChannelType ?? ChannelType.TextAndVoice,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return Ok(new CreateChannelResponseDto
        {
            ChannelId = response.ChannelId,
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListChannels(
        Guid serverId,
        Guid channelGroupId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var src = await mediator.Send(new ListChannelsQuery
        {
            ChannelGroupId = channelGroupId,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        var items = await src.Select(c => new ListChannelsResponseItemDto
        {
            ChannelId = c.ChannelId,
            Name = c.Name,
            ChannelType = c.ChannelType,
            ChannelGroupId = c.ChannelGroupId,
        }).ToListAsync(cancellationToken);

        return Ok(new ListChannelsResponseDto
        {
            Channels = items,
        });
    }

    [HttpPut("{channelId:guid}/name")]
    public async Task<IActionResult> RenameChannel(
        Guid serverId,
        Guid channelGroupId,
        Guid channelId,
        [FromBody] RenameChannelRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new RenameChannelCommand
        {
            ChannelId = channelId,
            NewName = requestDto.Name,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return NoContent();
    }

    [HttpPut("{channelId:guid}/type")]
    public async Task<IActionResult> UpdateChannelType(
        Guid serverId,
        Guid channelGroupId,
        Guid channelId,
        [FromBody] UpdateChannelTypeRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new UpdateChannelTypeCommand
        {
            ChannelId = channelId,
            ChannelType = requestDto.ChannelType,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{channelId:guid}")]
    public async Task<IActionResult> DeleteChannel(
        Guid serverId,
        Guid channelGroupId,
        Guid channelId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new DeleteChannelCommand
        {
            ChannelId = channelId,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return NoContent();
    }
}
