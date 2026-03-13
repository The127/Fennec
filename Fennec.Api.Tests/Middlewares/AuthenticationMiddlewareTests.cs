using Fennec.Api.Middlewares;
using Fennec.Api.Security;
using Fennec.Api.Services;
using Microsoft.AspNetCore.Http;
using NodaTime;
using NSubstitute;

namespace Fennec.Api.Tests.Middlewares;

public class AuthenticationMiddlewareTests
{
    private readonly IKeyService _keyService = Substitute.For<IKeyService>();
    private readonly IClockService _clockService = Substitute.For<IClockService>();
    private readonly Instant _now = Instant.FromUtc(2026, 3, 13, 12, 0);

    public AuthenticationMiddlewareTests()
    {
        _clockService.GetCurrentInstant().Returns(_now);
    }

    private AuthenticationMiddleware CreateMiddleware() => new(_keyService, _clockService);

    [Fact]
    public async Task Federation_endpoint_sets_auth_principal_after_successful_verification()
    {
        var instanceUrl = "https://remote.example.com";
        var timestamp = _now.ToUnixTimeSeconds().ToString();

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/federation/test";
        context.Request.Headers["X-Timestamp"] = timestamp;
        context.Request.Headers["X-Signature"] = "valid-signature";
        context.Request.Headers["X-Instance"] = instanceUrl;

        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(new FederationAuthAttribute()),
            "test");
        context.SetEndpoint(endpoint);

        var middleware = CreateMiddleware();
        var nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        Assert.True(context.Items.ContainsKey(AuthenticationMiddleware.AuthPrincipalKey),
            "AuthPrincipalKey should be set after federation auth succeeds");

        var principal = context.Items[AuthenticationMiddleware.AuthPrincipalKey] as IAuthPrincipal;
        Assert.NotNull(principal);
        Assert.Equal(instanceUrl, principal.Issuer);
    }
}
