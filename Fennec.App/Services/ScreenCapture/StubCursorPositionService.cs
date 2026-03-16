using Fennec.App.Messages;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// Stub implementation for platforms without native cursor tracking yet (Windows, macOS).
/// </summary>
public class StubCursorPositionService : ICursorPositionService
{
    public event Action<float, float, CursorType, bool>? OnCursorChanged;

    public void Start(CaptureTarget target) { }
    public void Stop() { }
}
