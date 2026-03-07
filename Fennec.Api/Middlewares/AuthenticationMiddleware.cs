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

    public record AuthenticationModel : IAuthPrincipal
    {
        public required Guid Id { get; init; }
        public required bool IsLocal { get; init; }
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.Any(m => m is AllowAnonymousAttribute) ??
            false)
        {
            context.Items[AuthPrincipalKey] = new AuthenticationModel
            {
                Id = Guid.Empty,
                IsLocal = false,
            };
            
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