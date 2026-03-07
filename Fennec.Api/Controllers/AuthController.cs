using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Services;
using Fennec.Api.Utils;
using Fennec.Shared.Dtos;
using Fennec.Shared.Dtos.Auth;
using HttpExceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : FennecControllerBase
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
        });
        
        return Ok(new LoginResponseDto
        {
            SessionToken = loginResponse.Token.Value,
        });
    }

    [AllowAnonymous]
    [HttpPost("public-token")]
    public async Task<IActionResult> GetPublicToken(
        [FromBody] GetPublicTokenRequestDto requestDto,
        [FromServices] IKeyService keyService,
        [FromServices] FennecDbContext dbContext
    )
    {
        var authorizationHeader = Request.Headers.GetAuthorizationHeader();
        var sessionToken = authorizationHeader.Match(
            _ => throw new HttpUnauthorizedException("Expected session token"), 
            sessionToken => sessionToken 
        );
        
        var session = dbContext
            .Set<Session>()
            .Include(x => x.User)
            .SingleOrDefault(x => x.Token == sessionToken.Value);

        if (session is null)
        {
            throw new HttpUnauthorizedException("Invalid token");      
        }
        
        return Ok(new GetPublicTokenResponseDto
        {
            Token = keyService.GetSignedToken(session.User, requestDto.Audience),
        });
    }
}