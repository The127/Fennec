using System.Diagnostics;
using Fennec.Api.Security;
using Fennec.Api.Services;
using Fennec.Api.Utils;
using HttpExceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;

namespace Fennec.Api.Middlewares;

public class AuthenticationMiddleware(IKeyService keyService) : IMiddleware
{
    public const string AuthPrincipalKey = nameof(AuthPrincipalKey);

    public record AnonymousUser : IAuthPrincipal
    {
        public Guid Id => throw new UnreachableException("Anonymous user info accessed");
        public string Name => throw new UnreachableException("Anonymous user info accessed");
        public string Issuer => throw new UnreachableException("Anonymous user info accessed");
        public bool IsLocal => throw new UnreachableException("Anonymous user info accessed");
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.Any(m => m is AllowAnonymousAttribute) ??
            false)
        {
            context.Items[AuthPrincipalKey] = new AnonymousUser();
            await next.Invoke(context);
            return;
        }

        var authorizationHeader = context.Request.Headers.GetAuthorizationHeader();

        var authPrincipalFactory = authorizationHeader.Match<Func<CancellationToken, Task<IAuthPrincipal>>>(
            bearerToken => async cancellationToken => await keyService.VerifyTokenAsync(bearerToken, cancellationToken),
            _ => throw new HttpUnauthorizedException("Expected bearer token")
        );
        
        var authPrincipal = await authPrincipalFactory(context.RequestAborted);
        context.Items[AuthPrincipalKey] = authPrincipal;
        
        await next.Invoke(context);
    }
}