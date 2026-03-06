using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;

namespace Fennec.Api.Middlewares;

public class AuthenticationMiddleware : IMiddleware
{
    public record AuthenticationModel
    {
    }
    
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.Any(m => m is AllowAnonymousAttribute) ??
            false)
        {
            await next.Invoke(context);
            return;
        }
    }
}