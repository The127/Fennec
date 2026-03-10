using System.Text.Json.Serialization;
using Fennec.Api.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.FederationApi;

[ApiController]
[Route("federation/v1/server")]
public class FederationServerController : FederationControllerBase
{
    public record ServerJoinFederateRequestDto
    {
        [JsonPropertyName("serverId")]
        public required Guid ServerId { get; init; }

        [JsonPropertyName("userInfo")]
        public required RemoteUserInfoDto UserInfo { get; init; }
    }

    public record ServerJoinFederateResponseDto
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }
    }

    [HttpPost("join")]
    public async Task<IActionResult> JoinServer(
        [FromBody] ServerJoinFederateRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var joinServerFederateResponse = await mediator.Send(new JoinServerFederateCommand
        {
            ServerId = requestDto.ServerId,
            UserInfo = requestDto.UserInfo,
        }, cancellationToken);

        return Ok(new ServerJoinFederateResponseDto
        {
            Name = joinServerFederateResponse.Name,
        });
    }
}