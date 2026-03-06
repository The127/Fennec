using System.Text.Json.Serialization;
using Fennec.Api.Commands;
using Fennec.Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers;

[ApiController]
[Route("api/v1/server")]
public class ServerController : FennecControllerBase
{
    public class CreateServerRequestDto
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        
        [JsonPropertyName("visibility")]
        public required ServerVisibility Visibility { get; set; }
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateServer(
        [FromBody] CreateServerRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        await mediator.Send(new CreateServerCommand
        {
            Name = requestDto.Name,
            Visibility =  requestDto.Visibility,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);
        
        return NoContent();
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