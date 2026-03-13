using Fennec.App.Exceptions;
using Fennec.App.Services.Storage;
using Fennec.Client;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.App.Services.Auth;

public class AuthService(
    IAuthStore authStore,
    IClientFactory clientFactory,
    IExceptionHandler exceptionHandler,
    IDbPathProvider dbPathProvider) : IAuthService
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

        dbPathProvider.CurrentDbPath = dbPathProvider.GetDbPath(authSession.UserId);
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

        var dbPath = dbPathProvider.GetDbPath(session.UserId);
        await authStore.RemoveSessionAsync(session, cancellationToken);
        dbPathProvider.CurrentDbPath = null;

        if (File.Exists(dbPath))
        {
            try
            {
                File.Delete(dbPath);
            }
            catch (Exception ex)
            {
                exceptionHandler.Handle(ex, "Failed to delete database at {DbPath}", dbPath);
            }
        }
    }

    public async Task SwitchAccountAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        dbPathProvider.CurrentDbPath = dbPathProvider.GetDbPath(session.UserId);
        await authStore.SetCurrentSessionAsync(session.UserId, cancellationToken);
    }
}