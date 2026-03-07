using Fennec.Api.Commands;
using Fennec.Shared.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        Request.HttpContext.Response.Headers.Append("Authorization", "Bearer " + loginResponse.Token);
        
        return NoContent();
    }
}