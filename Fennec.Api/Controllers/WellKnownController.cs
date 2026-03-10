using Fennec.Api.Services;
using Fennec.Shared.Dtos.WellKnown;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers;

[ApiController]
[Route(".well-known/fennec")]
public class WellKnownController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public-key")]
    public Task<IActionResult> GetPublicKey(
        [FromServices] IKeyService keyService,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IActionResult>(Ok(new GetPublicKeyResponseDto
        {
            PublicKeyPem = keyService.PublicKeyPem,
        }));
    }
}