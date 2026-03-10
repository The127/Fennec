using Fennec.Api.Middlewares;
using Fennec.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.UserApi;

public class UserControllerBase : ControllerBase
{
    protected IAuthPrincipal AuthPrincipal => (IAuthPrincipal)HttpContext.Items[AuthenticationMiddleware.AuthPrincipalKey]!;
}