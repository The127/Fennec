using System.Text.Json.Serialization;
using Fennec.Api.Commands;
using Fennec.Api.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.FederationApi;

[ApiController]
[FederationAuth]
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

    public record ServerRedeemInviteFederateRequestDto
    {
        [JsonPropertyName("inviteCode")]
        public required string InviteCode { get; init; }

        [JsonPropertyName("userInfo")]
        public required RemoteUserInfoDto UserInfo { get; init; }
    }

    public record ServerRedeemInviteFederateResponseDto
    {
        [JsonPropertyName("serverId")]
        public required Guid ServerId { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }
    }

    [HttpPost("invite/redeem")]
    public async Task<IActionResult> RedeemInvite(
        [FromBody] ServerRedeemInviteFederateRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var response = await mediator.Send(new JoinServerFederateCommand
        {
            InviteCode = requestDto.InviteCode,
            UserInfo = requestDto.UserInfo,
            InstanceUrl = AuthPrincipal.Issuer,
        }, cancellationToken);

        return Ok(new ServerRedeemInviteFederateResponseDto
        {
            ServerId = response.ServerId,
            Name = response.Name,
        });
    }
}
