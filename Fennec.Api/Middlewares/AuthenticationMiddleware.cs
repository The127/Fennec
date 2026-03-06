using Fennec.Api.Models;
using Fennec.Api.Security;
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
        
        var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authorizationHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Authorization header is missing");
            return;
        }
        
        var parts = authorizationHeader.Split(' ');
        if (parts is not ["Bearer", _])
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Invalid authorization header format");
            return;
        }
        
        var token = parts[1];
        
        var dbContext = context.RequestServices.GetRequiredService<FennecDbContext>();
        var session = dbContext
            .Set<Session>()
            .SingleOrDefault(x => x.Token == token);

        if (session is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid token");
            return;       
        }
        
        context.Items[AuthPrincipalKey] = new AuthenticationModel { Id = session.UserId };
        
        await next.Invoke(context);
    }
}