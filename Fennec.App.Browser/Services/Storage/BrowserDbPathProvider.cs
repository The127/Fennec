using Fennec.App.Services.Storage;

namespace Fennec.App.Browser.Services.Storage;

public class BrowserDbPathProvider : IDbPathProvider
{
    public string GetDbPath(Guid userId)
    {
        return $"app_{userId}.db";
    }

    public string? CurrentDbPath { get; set; }
}
