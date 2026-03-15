using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// Stub implementation for platforms without native screen capture support yet (Windows, macOS).
/// </summary>
public class StubScreenCaptureService(ILogger<StubScreenCaptureService> logger) : IScreenCaptureService
{
    public bool IsCapturing => false;

    public Task<List<CaptureTarget>> GetAvailableTargetsAsync()
    {
        logger.LogWarning("ScreenCapture: Not implemented on this platform");
        return Task.FromResult(new List<CaptureTarget>());
    }

    public Task StartAsync(CaptureTarget target, Action<byte[], int, int> onFrame)
    {
        logger.LogWarning("ScreenCapture: Not implemented on this platform");
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;
}
