using Fennec.App.Services.Storage;

namespace Fennec.App.Desktop.Services.Storage;

public class DesktopDbPathProvider : IDbPathProvider
{
    public string GetDbPath(Guid userId)
    {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            App.AppName);
        return Path.Combine(basePath, $"app_{userId}.db");
    }

    public string? CurrentDbPath { get; set; }
}
