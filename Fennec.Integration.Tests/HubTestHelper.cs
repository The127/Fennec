using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.IdentityModel.Tokens;

namespace Fennec.Integration.Tests;

public static class HubTestHelper
{
    private static readonly RSA TokenSigningKey = RSA.Create(2048);

    /// <summary>
    /// Creates a JWT that the MessageHub's GetCallerIdentity() can parse.
    /// The hub reads claims without validating the signature, so any valid JWT format works.
    /// </summary>
    public static string CreateTestJwt(Guid userId, string username, string issuerUrl)
    {
        var credentials = new SigningCredentials(
            new RsaSecurityKey(TokenSigningKey), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: issuerUrl,
            audience: issuerUrl,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, username),
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates and starts a SignalR hub connection through the test server using long-polling.
    /// </summary>
    public static async Task<HubConnection> ConnectToHubAsync(TestApiFactory factory, string jwt)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"{factory.Server.BaseAddress}hubs/messages", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(jwt);
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }

    /// <summary>
    /// Waits for a hub event, returning the result or throwing on timeout.
    /// </summary>
    public static async Task<T> WaitForEventAsync<T>(
        TaskCompletionSource<T> tcs,
        TimeSpan? timeout = null)
    {
        var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        try
        {
            return await tcs.Task;
        }
        finally
        {
            await registration.DisposeAsync();
        }
    }
}
