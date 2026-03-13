using System.Text.Json;
using System.Text.Json.Serialization;
using Fennec.App.Services.Auth;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Browser.Services.Auth;

public class BrowserAuthStore(ILogger<BrowserAuthStore> logger) : IAuthStore
{
    public class AuthConfig
    {
        [JsonPropertyName("sessions")]
        public List<AuthSession> Sessions { get; set; } = [];

        [JsonPropertyName("currentUserId")]
        public Guid? CurrentUserId { get; set; }
    }

    private const string StorageKey = "fennec_auth_config";

    public async Task<AuthSession?> GetCurrentAuthSessionAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        return config.Sessions.FirstOrDefault(x => x.UserId == config.CurrentUserId);
    }

    public async Task<List<AuthSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        return config.Sessions;
    }

    public async Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);

        if (!config.Sessions.Any(x => x.UserId == session.UserId))
        {
            config.Sessions.Add(session);
        }

        config.CurrentUserId = session.UserId;

        await SaveConfigAsync(config, cancellationToken);
    }

    public async Task RemoveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);

        config.Sessions.RemoveAll(x => x.UserId == session.UserId);

        if (config.CurrentUserId == session.UserId)
        {
            config.CurrentUserId = null;
        }

        await SaveConfigAsync(config, cancellationToken);
    }

    public async Task SetCurrentSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        config.CurrentUserId = userId;
        await SaveConfigAsync(config, cancellationToken);
    }

    private Task<AuthConfig> LoadConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = LocalStorageInterop.GetItem(StorageKey);
            if (json is null)
                return Task.FromResult(new AuthConfig());

            var config = JsonSerializer.Deserialize<AuthConfig>(json);
            return Task.FromResult(config ?? new AuthConfig());
        }
        catch (JsonException)
        {
            logger.LogWarning("Auth config in localStorage is outdated or corrupted and has been cleared. Please log in again.");
            LocalStorageInterop.RemoveItem(StorageKey);
            return Task.FromResult(new AuthConfig());
        }
    }

    private Task SaveConfigAsync(AuthConfig config, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(config);
        LocalStorageInterop.SetItem(StorageKey, json);
        return Task.CompletedTask;
    }
}