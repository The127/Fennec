using System.Diagnostics;
using Fennec.Api.Security;
using Fennec.Api.Services;
using Fennec.Api.Utils;
using HttpExceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;

namespace Fennec.Api.Middlewares;

public class AuthenticationMiddleware(IKeyService keyService, IClockService clockService) : IMiddleware
{
    public const string AuthPrincipalKey = nameof(AuthPrincipalKey);

    public record AnonymousUser : IAuthPrincipal
    {
        public Guid Id => throw new UnreachableException("Anonymous user info accessed");
        public string Name => throw new UnreachableException("Anonymous user info accessed");
        public string Issuer => throw new UnreachableException("Anonymous user info accessed");
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.Any(m => m is FederationAuthAttribute) ?? false)
        {
            await HandleFederationEndpoint(context);
        }

        if (endpoint?.Metadata.Any(m => m is UserAuthAttribute) ?? false)
        {
            await HandleUserEndpoint(context);
        }

        await next.Invoke(context);
    }

    private async Task HandleFederationEndpoint(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.Any(m => m is AllowAnonymousAttribute) ?? false)
        {
            context.Items[AuthPrincipalKey] = new AnonymousUser();
            return;
        }

        var timestampHeader = context.Request.Headers["X-Timestamp"].FirstOrDefault();
        if (timestampHeader is null)
        {
            throw new HttpBadRequestException("Missing timestamp header");
        }

        var timestamp = long.Parse(timestampHeader);
        if (timestamp < clockService.GetCurrentInstant().ToUnixTimeSeconds() - 30)
        {
            throw new HttpBadRequestException("Timestamp is too old");
        }

        var signatureHeader = context.Request.Headers["X-Signature"].FirstOrDefault();
        if (signatureHeader is null)
        {
            throw new HttpBadRequestException("Missing signature header");
        }
        
        var instanceUrl = context.Request.Headers["X-Instance"].FirstOrDefault();
        if (instanceUrl is null)
        {
            throw new HttpBadRequestException("Missing instance URL header");
        }
        
        var body = "";
        if (context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering(); // allow reading the body multiple times
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync(context.RequestAborted);
            context.Request.Body.Position = 0; // reset stream
        }
        
        var payload = $"{context.Request.Method}\n{context.Request.GetEncodedPathAndQuery()}\n{timestamp}\n{body}";
        
        await keyService.VerifyPayloadAsync(payload, signatureHeader, instanceUrl, context.RequestAborted);
    }

    private async Task HandleUserEndpoint(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.Any(m => m is AllowAnonymousAttribute) ?? false)
        {
            context.Items[AuthPrincipalKey] = new AnonymousUser();
            return;
        }

        var authorizationHeader = context.Request.Headers.GetAuthorizationHeader();

        var authPrincipalFactory = authorizationHeader.Match<Func<CancellationToken, Task<IAuthPrincipal>>>(
            bearerToken => async cancellationToken => await keyService.VerifyTokenAsync(bearerToken, cancellationToken),
            _ => throw new HttpUnauthorizedException("Expected bearer token")
        );

        var authPrincipal = await authPrincipalFactory(context.RequestAborted);
        context.Items[AuthPrincipalKey] = authPrincipal;
    }
}