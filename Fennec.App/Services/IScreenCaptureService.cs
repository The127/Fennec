namespace Fennec.App.Services;

public enum CaptureTargetKind
{
    Screen,
    Window,
}

public record CaptureTarget(CaptureTargetKind Kind, string Id, string Name, int Width = 0, int Height = 0);

public interface IScreenCaptureService
{
    Task<List<CaptureTarget>> GetAvailableTargetsAsync();
    Task StartAsync(CaptureTarget target, Action<byte[], int, int> onFrame);
    Task StopAsync();
    bool IsCapturing { get; }
}
