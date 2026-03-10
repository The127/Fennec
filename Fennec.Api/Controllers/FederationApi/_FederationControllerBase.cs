using Fennec.Api.Middlewares;
using Fennec.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.FederationApi;

public class FederationControllerBase : ControllerBase
{
    protected IAuthPrincipal AuthPrincipal => (IAuthPrincipal)HttpContext.Items[AuthenticationMiddleware.AuthPrincipalKey]!;
}