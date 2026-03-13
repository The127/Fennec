namespace Fennec.App.Services.Auth;

public interface IAuthStore
{
    Task<AuthSession?> GetCurrentAuthSessionAsync(CancellationToken cancellationToken = default);
    Task<List<AuthSession>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default);
    Task RemoveSessionAsync(AuthSession session, CancellationToken cancellationToken = default);
    Task SetCurrentSessionAsync(Guid userId, CancellationToken cancellationToken = default);
}