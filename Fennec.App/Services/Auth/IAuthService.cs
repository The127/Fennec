namespace Fennec.App.Services.Auth;

public interface IAuthService
{
    Task<AuthSession?> LoginAsync(string username, string password, string instanceUrl, CancellationToken cancellationToken);
    Task RegisterAsync(string username, string? displayName, string password, string instanceUrl, CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task SwitchAccountAsync(AuthSession session, CancellationToken cancellationToken = default);
}