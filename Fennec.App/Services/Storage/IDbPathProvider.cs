namespace Fennec.App.Services.Storage;

public interface IDbPathProvider
{
    string GetDbPath(Guid userId);
    string? CurrentDbPath { get; set; }
}
