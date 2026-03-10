using Fennec.Api.Commands;
using Fennec.Shared.Dtos.Server;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers;

[ApiController]
[Route("api/v1/server")]
public class ServerController : FennecControllerBase
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

    [HttpPost("{server:guid}/join")]
    public async Task<IActionResult> JoinServer(
        Guid server,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new JoinServerCommand
        {
            ServerId = server,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);
        
        return NoContent();
    }
}