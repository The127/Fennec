using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fennec.App.Services.Auth;

namespace Fennec.App.Desktop.Services.Auth;

public class DesktopAuthStore : IAuthStore
{
    public class AuthConfig
    {
        [JsonPropertyName("sessions")]
        public List<AuthSession> Sessions { get; set; } = [];
        
        [JsonPropertyName("currentUserId")]
        public Guid? CurrentUserId { get; set; }
    }

    private string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        App.AppName, "auth.json");

    public Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        var config = LoadConfigAsync(cancellationToken).Result;
        return Task.FromResult(config.CurrentUserId);
    }

    public async Task<List<AuthSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        return config.Sessions;
    }

    public async Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        
        if (config.Sessions.Any(x => x.UserId == session.UserId))
        {
            return;
        }

        config.Sessions.Add(session);
        config.CurrentUserId = session.UserId;

        await SaveConfigAsync(config, cancellationToken);
    }

    public Task RemoveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        var config = LoadConfigAsync(cancellationToken).Result;
        
        config.Sessions.RemoveAll(x => x.UserId == session.UserId);

        if (config.CurrentUserId == session.UserId)
        {
            config.CurrentUserId = null;
        }
        
        return SaveConfigAsync(config, cancellationToken);
    }

    private async Task<AuthConfig> LoadConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var authConfigFile = File.OpenRead(ConfigPath);
            var authConfig =
                await JsonSerializer.DeserializeAsync<AuthConfig>(authConfigFile, cancellationToken: cancellationToken);
            return authConfig ?? new AuthConfig();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new AuthConfig();
        }
    }

    private async Task SaveConfigAsync(AuthConfig config, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.GetDirectoryName(ConfigPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ??
                                      throw new UnreachableException("config path is null"));
        }

        await using var authConfigFile = File.OpenWrite(ConfigPath);
        await JsonSerializer.SerializeAsync(authConfigFile, config, cancellationToken: cancellationToken);
    }
}