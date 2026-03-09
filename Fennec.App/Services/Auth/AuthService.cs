using Fennec.Client;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.App.Services.Auth;

public class AuthService(IAuthStore authStore) : IAuthService
{
    public async Task<AuthSession?> Login(string username, string password, string instanceUrl, CancellationToken cancellationToken)
    {
        var client = new ClientFactory(instanceUrl).Create();

        var response = await client.Auth.LoginAsync(new LoginRequestDto
        {
            Name = username,
            Password = password,
        }, cancellationToken);

        var authSession = new AuthSession
        {
            SessionToken = response.SessionToken,
            UserId = response.UserId,
        };
        
        await authStore.SaveSessionAsync(authSession, cancellationToken);
        return authSession;
    }
}