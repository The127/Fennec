using System.Text.Json.Serialization;
using Fennec.Api.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    public class RegisterUserRequest
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("password")]
        public required string Password { get; set; }
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser(
        [FromBody] RegisterUserRequest request,
        [FromServices] IMediator mediator, 
        CancellationToken cancellationToken = default
    )
    {
        await mediator.Send(new RegisterUserCommand
        {
            Name = request.Name,
            Password = request.Password
        }, cancellationToken);

        return NoContent();
    }
}