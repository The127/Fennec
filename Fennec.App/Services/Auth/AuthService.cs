using Fennec.App.Exceptions;
using Fennec.Client;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.App.Services.Auth;

public class AuthService(IAuthStore authStore, IClientFactory clientFactory, IExceptionHandler exceptionHandler) : IAuthService
{
    private static string StripScheme(string url)
        => url.Replace("https://", "").Replace("http://", "");

    public async Task<AuthSession?> LoginAsync(string username, string password, string instanceUrl, CancellationToken cancellationToken)
    {
        var client = clientFactory.Create();

        var response = await client.Auth.LoginAsync(instanceUrl, new LoginRequestDto
        {
            Name = username,
            Password = password,
        }, cancellationToken);

        client.SetHomeSession(instanceUrl, response.SessionToken);

        var authSession = new AuthSession
        {
            Url = instanceUrl,
            SessionToken = response.SessionToken,
            UserId = response.UserId,
            Username = username,
        };

        await authStore.SaveSessionAsync(authSession, cancellationToken);
        return authSession;
    }

    public async Task RegisterAsync(string username, string? displayName, string password, string instanceUrl, CancellationToken cancellationToken)
    {
        var client = clientFactory.Create();

        await client.Auth.RegisterAsync(instanceUrl, new RegisterUserRequestDto
        {
            Name = username,
            DisplayName = displayName,
            Password = password,
        }, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var session = await authStore.GetCurrentAuthSessionAsync(cancellationToken);
        if (session is null) return;

        try
        {
            var client = clientFactory.Create();
            client.SetHomeSession(session.Url, session.SessionToken);
            await client.Auth.LogoutAsync(session.Url, cancellationToken);
        }
        catch (Exception ex)
        {
            exceptionHandler.Handle(ex, "Failed to logout from {Url}", session.Url);
            // Server unreachable - the token will expire on its own.
        }

        await authStore.RemoveSessionAsync(session, cancellationToken);
    }
}