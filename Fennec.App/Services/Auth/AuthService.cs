using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Fennec.Client;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.App.Services.Auth;

public class AuthService(IAuthStore authStore) : IAuthService
{
    public async Task<AuthSession?> LoginAsync(string username, string password, string instanceUrl, CancellationToken cancellationToken)
    {
        var client = new ClientFactory(instanceUrl).Create();

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

    public async Task RegisterAsync(string username, string password, string instanceUrl, CancellationToken cancellationToken)
    {
        var client = new ClientFactory(instanceUrl).Create();

        await client.Auth.RegisterAsync(new RegisterUserRequestDto
        {
            Name = username,
            Password = password,
        }, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var session = await authStore.GetCurrentAuthSessionAsync(cancellationToken);
        if (session is null) return;

        var client = new ClientFactory(session.Url)
            .WithSessionToken(session.SessionToken)
            .Create();

        await client.Auth.LogoutAsync(cancellationToken);
        await authStore.RemoveSessionAsync(session, cancellationToken);
    }
}