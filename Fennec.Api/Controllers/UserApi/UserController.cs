using Fennec.Api.Security;
using Fennec.Shared.Dtos.User;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.UserApi;

[ApiController]
[UserAuth]
[Route("api/v1/users")]
public class UserController : UserControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        return Ok(new MeResponseDto
        {
            Id = AuthPrincipal.Id,
            Name = AuthPrincipal.Name,
        });
    }
}