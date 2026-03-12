using System.Diagnostics;
using System.Text.Json;
using Fennec.App.Services;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Desktop.Services;

public class DesktopSettingsStore(ILogger<DesktopSettingsStore> logger) : ISettingsStore
{
    private string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        App.AppName, "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var file = File.OpenRead(ConfigPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(file, cancellationToken: cancellationToken);
            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            logger.LogWarning("Settings file at {ConfigPath} is corrupted and has been deleted.", ConfigPath);
            File.Delete(ConfigPath);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory ??
                                      throw new UnreachableException("config path is null"));
        }

        await using var file = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(file, settings, cancellationToken: cancellationToken);
    }
}
