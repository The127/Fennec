using Fennec.Client;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.App.Services.Auth;

public class AuthService(IAuthStore authStore, IClientFactory clientFactory) : IAuthService
{
    private static string StripScheme(string url)
        => url.Replace("https://", "").Replace("http://", "");

    public async Task<AuthSession?> LoginAsync(string username, string password, string instanceUrl, CancellationToken cancellationToken)
    {
        instanceUrl = StripScheme(instanceUrl);
        var client = clientFactory.Create(instanceUrl);

        var response = await client.Auth.LoginAsync(new LoginRequestDto
        {
            Name = username,
            Password = password,
        }, cancellationToken);

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
        instanceUrl = StripScheme(instanceUrl);
        var client = clientFactory.Create(instanceUrl);

        await client.Auth.RegisterAsync(new RegisterUserRequestDto
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
            var client = clientFactory.Create(StripScheme(session.Url), session.SessionToken);
            await client.Auth.LogoutAsync(cancellationToken);
        }
        catch
        {
            // Server unreachable - the token will expire on its own.
        }

        await authStore.RemoveSessionAsync(session, cancellationToken);
    }
}