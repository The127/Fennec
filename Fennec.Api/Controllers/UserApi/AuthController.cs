using Fennec.Api.Commands;
using Fennec.Api.Security;
using Fennec.Api.Utils;
using Fennec.Shared.Dtos.Auth;
using HttpExceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.UserApi;

[UserAuth]
[ApiController]
[Route("api/v1/auth")]
public class AuthController : UserControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser(
        [FromBody] RegisterUserRequestDto requestDto,
        [FromServices] IMediator mediator, 
        CancellationToken cancellationToken = default
    )
    {
        await mediator.Send(new RegisterUserCommand
        {
            Name = requestDto.Name,
            DisplayName = null,
            Password = requestDto.Password
        }, cancellationToken);

        return NoContent();
    }
    
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> LoginUser(
        [FromBody] LoginRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken = default
    )
    {
        var loginResponse = await mediator.Send(new LoginCommand
        {
            Name = requestDto.Name,
            Password = requestDto.Password
        }, cancellationToken);
        
        return Ok(new LoginResponseDto
        {
            SessionToken = loginResponse.Token.Value,
            UserId = loginResponse.UserId,
        });
    }

    [AllowAnonymous]
    [HttpPost("public-token")]
    public async Task<IActionResult> GetPublicToken(
        [FromBody] GetPublicTokenRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var authorizationHeader = Request.Headers.GetAuthorizationHeader();
        var sessionToken = authorizationHeader.Match(
            _ => throw new HttpUnauthorizedException("Expected session token"), 
            sessionToken => sessionToken 
        );

        var publicTokenResponse = await mediator.Send(new CreatePublicTokenCommand
        {
            Token = sessionToken,
            Audience = requestDto.Audience,
        }, cancellationToken);
        
        return Ok(new GetPublicTokenResponseDto
        {
            Token = publicTokenResponse.Token,
        });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var authorizationHeader = Request.Headers.GetAuthorizationHeader();
        var sessionToken = authorizationHeader.Match(
            _ => throw new HttpUnauthorizedException("Expected session token"), 
            sessionToken => sessionToken 
        );
        
        await mediator.Send(new LogoutCommand
        {
            Token = sessionToken,
        }, cancellationToken);
        
        return NoContent();
    }
}