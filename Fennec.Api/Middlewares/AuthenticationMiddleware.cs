using Fennec.Api.Security;
using Fennec.Api.Utils;
using HttpExceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;

namespace Fennec.Api.Middlewares;

public class AuthenticationMiddleware : IMiddleware
{
    public const string AuthPrincipalKey = nameof(AuthPrincipalKey);

    public record AuthenticationModel : IAuthPrinciple
    {
        public required Guid Id { get; init; }
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.Any(m => m is AllowAnonymousAttribute) ??
            false)
        {
            context.Items[AuthPrincipalKey] = new AuthenticationModel { Id = Guid.Empty };
            await next.Invoke(context);
            return;
        }

        var authorizationHeader = context.Request.Headers.GetAuthorizationHeader();

        var authPrincipal = authorizationHeader.Match(
            bearerToken => new AuthenticationModel { Id = Guid.NewGuid() },
            sessionToken => throw new HttpUnauthorizedException("Expected bearer token")
        );

        context.Items[AuthPrincipalKey] = authPrincipal;
        await next.Invoke(context);
    }
}