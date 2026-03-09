namespace Fennec.App.Services.Auth;

public interface IAuthService
{
    Task<AuthSession?> Login(string username, string password, string instanceUrl, CancellationToken cancellationToken);
}