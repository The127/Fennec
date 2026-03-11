using Fennec.Api.Queries;
using Fennec.Api.Security;
using Fennec.Api.Utils;
using Fennec.Shared.Dtos.User;
using HttpExceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.UserApi;

[ApiController]
[UserAuth]
[Route("api/v1/users")]
public class UserController : UserControllerBase
{
    [AllowAnonymous]
    [HttpGet("me")]
    public async Task<IActionResult> Me(
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var authorizationHeader = Request.Headers.GetAuthorizationHeader();
        var sessionToken = authorizationHeader.Match(
            _ => throw new HttpUnauthorizedException("Expected session token"), 
            sessionToken => sessionToken 
        );
        
        var meResponse = await mediator.Send(
            new MeQuery
            {
                Token = sessionToken,
            }, cancellationToken);
        
        return Ok(new MeResponseDto
        {
            Id = meResponse.UserId,
            Name = meResponse.Username,
            DisplayName = meResponse.DisplayName ?? meResponse.Username,
        });
    }
}