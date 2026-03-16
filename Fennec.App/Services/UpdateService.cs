using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services;

public record UpdateInfo(string Version, string DownloadUrl);

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task DownloadAndApplyAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default);
}

public class UpdateService(ILogger<UpdateService> logger) : IUpdateService
{
    private const string Repo = "The127/Fennec";

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            logger.LogInformation("Skipping update check in Development environment");
            return null;
        }

        var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
        var url = $"https://api.github.com/repos/{Repo}/releases/latest";
        logger.LogInformation("Checking for updates at {Url} (current version: {CurrentVersion})", url, currentVersion);

        try
        {
            using var http = CreateHttpClient();
            var release = await http.GetFromJsonAsync(url, GithubJsonContext.Default.GithubRelease);

            if (release is null)
            {
                logger.LogWarning("GitHub returned an empty release response");
                return null;
            }

            var latestVersion = ParseVersion(release.TagName);
            logger.LogInformation("Latest release: {Tag} (parsed: {LatestVersion}), current: {CurrentVersion}",
                release.TagName, latestVersion, currentVersion);

            if (latestVersion <= currentVersion)
            {
                logger.LogInformation("Already up to date");
                return null;
            }

            var assetName = GetAssetName();
            var asset = release.Assets.FirstOrDefault(a => a.Name == assetName);
            if (asset is null)
            {
                logger.LogWarning("No matching asset {AssetName} in release {Tag}", assetName, release.TagName);
                return null;
            }

            logger.LogInformation("Update found: {CurrentVersion} → {LatestVersion}, asset: {AssetName}",
                currentVersion, latestVersion, assetName);
            return new UpdateInfo(release.TagName.TrimStart('v'), asset.BrowserDownloadUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Update check failed");
            return null;
        }
    }

    public async Task DownloadAndApplyAsync(UpdateInfo update, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var exePath = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Cannot determine current executable path");
        var exeDir = Path.GetDirectoryName(exePath)!;
        var assetName = GetAssetName();
        var downloadPath = Path.Combine(exeDir, assetName);

        logger.LogInformation("Downloading v{Version} from {Url} to {DownloadPath}",
            update.Version, update.DownloadUrl, downloadPath);

        using var http = CreateHttpClient();
        using var response = await http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        logger.LogInformation("Download size: {TotalBytes} bytes", totalBytes < 0 ? "unknown" : totalBytes.ToString());

        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(downloadPath);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;
            if (totalBytes > 0) progress?.Report((double)bytesRead / totalBytes);
        }

        dst.Close();
        logger.LogInformation("Download complete ({BytesRead} bytes), installing", bytesRead);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Can't overwrite a running .exe on Windows — rename it out of the way first
            var oldPath = exePath + ".old";
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(exePath, oldPath);
            File.Move(downloadPath, exePath);
        }
        else
        {
            // On Unix, rename() replaces the path atomically even while the binary is running
            File.Move(downloadPath, exePath, overwrite: true);
            File.SetUnixFileMode(exePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        logger.LogInformation("Update installed, restarting");
        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        Environment.Exit(0);
    }

    // Call once at startup to remove the leftover .old binary from a previous Windows update
    public static void CleanupStaleBinary()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var exePath = Environment.ProcessPath;
        if (exePath is null) return;
        try { File.Delete(exePath + ".old"); }
        catch { /* best effort */ }
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Add("User-Agent", "Fennec-App");
        return http;
    }

    private static Version ParseVersion(string tag)
        => Version.TryParse(tag.TrimStart('v'), out var v) ? v : new Version(0, 0, 0);

    private static string GetAssetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "fennec-win-x64.exe";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "fennec-osx-arm64"
                : "fennec-osx-x64";
        return "fennec-linux-x64";
    }
}

internal record GithubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("assets")] List<GithubAsset> Assets);

internal record GithubAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);

[JsonSerializable(typeof(GithubRelease))]
internal partial class GithubJsonContext : JsonSerializerContext;
