using Fennec.Api.Controllers.UserApi;
using Fennec.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.FederationApi;

[ApiController]
[Route("federation/v1/server")]
public class FederationServerController : FederationControllerBase
{
    [HttpPost("join")]
    public async Task<IActionResult> JoinServer()
    {
        return Ok();
    }
}