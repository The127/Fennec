using Fennec.Api.Middlewares;
using Fennec.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers;

public class FennecControllerBase : ControllerBase
{
    protected IAuthPrincipal AuthPrincipal => (AuthenticationMiddleware.AuthenticationModel)HttpContext.Items[AuthenticationMiddleware.AuthPrincipalKey]!;
    
}